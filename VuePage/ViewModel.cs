﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vue
{
    public partial class ViewModel : IDisposable
    {
        #region Properties/Virtual Methods

        private JavascriptBuilder _js = new JavascriptBuilder();
        private JsonSerializerSettings _serializeSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            ContractResolver = VueContractResolver.Instance
        };

        /// <summary>
        /// Get instance of Javascript builder to be run after update/create vue instance
        /// </summary>
        protected JavascriptBuilder JS { get { return _js; } }

        /// <summary>
        /// Get current HttpContext
        /// </summary>
        protected HttpContext Context { get; private set; }

        /// <summary>
        /// Called after create instance and set Context object
        /// </summary>
        protected virtual void OnInit()
        {
        }

        /// <summary>
        /// In page call during initialize. In component, made ajax call when component are created
        /// </summary>
        protected virtual void OnCreated()
        {
        }

        #endregion

        #region RenderScript

        /// <summary>
        /// Execute on Vue are inside a page
        /// </summary>
        public virtual string RenderControl(string id, string content)
        {
            // created event are called when render initilize
            OnCreated();

            var writer = new StringBuilder();

            writer.Append("new Vue({\n");

            if (IsAjaxGetEnabled)
            {
                writer.Append("  created: function() {\n");
                writer.Append("     this.$registerPage(this);\n");
                writer.Append("  },\n");
            }

            if (_js.Length > 0)
            {
                writer.Append("  mounted: function() {\n");
                writer.Append(_js.ToString());
                writer.Append("\n  },\n");
            }

            RenderBody(writer, content);

            writer.AppendFormat("}}).$mount('#{0}');", id);

            return writer.ToString();
        }

        /// <summary>
        /// Render viewmodel as component
        /// </summary>
        public virtual string RenderComponent(string vpath, string content)
        {
            var writer = new StringBuilder();

            var props = this.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(x => x.GetCustomAttribute<PropAttribute>() != null)
                .Select(x => new { Prop = x.GetCustomAttribute<PropAttribute>().Name, Name = x.Name })
                .ToList();

            // checks if prop name are different from viewmodel field
            props.ForEach((x) => { if(x.Name == x.Prop) throw new ArgumentException("[Vue.Prop] name must be different from view model property"); });

            writer.Append("return {\n");
            writer.AppendFormat("  vpath: '{0}',\n", vpath);
            writer.AppendFormat("  props: [{0}],\n", string.Join(", ", props.Select(x => "'" + x.Prop + "'")));

            writer.Append("  created: function() {\n");
            writer.Append(string.Join("\n", props.Select(x => string.Format("    this.{0} = this.{1};", x.Name, x.Prop))));

            if (_js.Length > 0)
            {
                writer.Append(_js.ToString() + "\n");
            }

            // only call Created method if created was override in component
            var created = GetType().GetMethod("OnCreated", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if(created.GetBaseDefinition().DeclaringType != created.DeclaringType)
            {
                writer.Append("    this.$update('OnCreated', [], null, this);\n");
            }

            writer.Append("  },\n");

            RenderBody(writer, content);

            writer.Append("}\n");

            return writer.ToString();
        }

        private void RenderBody(StringBuilder writer, string content)
        {
            var model = JObject.FromObject(this);
            var template = ParseTemplate(content, out string mixin, out string style);

            writer.AppendFormat("  template: '{0}',\n", template);
            writer.AppendFormat("  data: function() {{ return {0}; }},\n", JsonConvert.SerializeObject(this, _serializeSettings));

            var methods = this.GetType()
                .GetMethods(BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(x => !x.IsSpecialName)
                .ToArray();

            if (methods.Length > 0)
            {
                writer.Append("  methods: {\n");

                foreach (var m in methods)
                {
                    // checks if method contains Script attribute (will run before call $update)
                    var pre = string.Join(";", m.GetCustomAttributes<PreScriptAttribute>(true)?.Select(x => x.Code + "\n        ") ?? new string[0]);
                    var post = string.Join(";", m.GetCustomAttributes<PostScriptAttribute>(true)?.Select(x => x.Code) ?? new string[0]);

                    // get all parameters without HttpPostFile parameters
                    var parameters = m.GetParameters()
                        .Where(x => x.ParameterType != typeof(HttpPostedFile) && x.ParameterType != typeof(List<HttpPostedFile>))
                        .Select(x => x.Name);

                    // get if any parameter are file(s)
                    var upload = m.GetParameters()
                        .Where(x => x.ParameterType == typeof(HttpPostedFile) || x.ParameterType == typeof(List<HttpPostedFile>))
                        .Select(x => x.Name)
                        .FirstOrDefault() ?? "null";

                    writer.AppendFormat("    '{0}': function({1}) {{\n      {2}this.$update('{0}', [{3}], {4}, this){5};\n    }},\n",
                        m.Name,
                        string.Join(", ", m.GetParameters().Select(x => x.Name)),
                        pre,
                        string.Join(", ", parameters),
                        upload,
                        post.Length > 0 ? ".then(function(vm) { (function() { " + post + " }).call(vm); });" : ";");
                }

                writer.Length -= 2;
                writer.Append("\n  },\n");
            }

            var computed = this.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(x => x.FieldType == typeof(Computed))
                .ToArray();

            if (computed.Length > 0)
            {
                writer.Append("  computed: {\n");

                foreach (var c in computed)
                {
                    writer.AppendFormat("    '{0}': function() {{\n      return ({1})(this);\n    }},\n",
                        c.Name,
                        ((Computed)c.GetValue(this)).Code);
                }

                writer.Length -= 2;
                writer.Append("\n  },\n");
            }

            // get all method marked with [Watch] attribute or ends with _Watch
            var watchs = this.GetType()
                .GetMethods(BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(x => x.Name.EndsWith("_Watch", StringComparison.InvariantCultureIgnoreCase) || x.GetCustomAttribute<WatchAttribute>() != null)
                .ToArray();

            if (watchs.Length > 0)
            {
                writer.Append("  watch: {\n");

                foreach (var w in watchs)
                {
                    // checks if method contains Script attribute (will run before call $update)
                    var script = w.GetCustomAttribute<PreScriptAttribute>(true)?.Code + "\n        ";

                    var name = w.GetCustomAttribute<WatchAttribute>()?.Name ?? w.Name.Substring(0, w.Name.LastIndexOf("_"));

                    writer.AppendFormat("    '{0}': {{\n      handler: function(v, o) {{\n        if (this.$updating) return false;\n        {2}this.$update('{1}', [v, o], null, this);\n      }},\n      deep: true\n    }},\n",
                        name, w.Name, script);
                }

                writer.Length -= 2;
                writer.Append("\n  },\n");
            }

            // if has style, add here
            if (!string.IsNullOrEmpty(style))
            {
                try
                {
                    var config = new dotless.Core.configuration.DotlessConfiguration
                    {
                        MinifyOutput = true,
                        Web = true,
                        MapPathsToWeb = true
                    };

                    var css = JavascriptBuilder.Encode(dotless.Core.LessWeb.Parse(style, config));

                    writer.AppendFormat("    'beforeCreate': function() {{ this.$addStyle('{0}'); }},\n", css);
                }
                catch (Exception ex)
                {
                    writer.AppendFormat("    'beforeCreate': function() {{ alert('{0}'); }},\n", HttpUtility.JavaScriptStringEncode(ex.Message));
                }
            }

            if(!string.IsNullOrEmpty(mixin))
            {
                writer.AppendFormat("    'mixins': [(function() {{ {0} }})() || {{}}],\n",
                    mixin);
            }

            // remove last ,\n
            writer.Length -= 2;
            writer.Append("\n");
        }

        #endregion

        #region Update Model

        internal string UpdateModel(string model, string method, JToken[] parameters, IList<HttpPostedFile> files)
        {
            JsonConvert.PopulateObject(model, this, _serializeSettings);

            ExecuteMethod(method, parameters, files);

            return RenderUpdate(model);
        }

        private void ExecuteMethod(string name, JToken[] parameters, IList<HttpPostedFile> files)
        {
            var method = this.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(x => x.Name == name)
                .Where(x => x.IsFamily || x.IsPublic)
                //.Where(x => x.GetParameters().Length == (parameters.Length + files.Count))
                .FirstOrDefault();

            if (method == null) throw new SystemException("Method " + name + " do not exists or are not public/protected or has not same parameters length");

            // test if method are decorated with [Roles("...")]
            var roleAttr = method.GetCustomAttribute<RoleAttribute>();

            if (roleAttr != null)
            {
                AuthorizeMethod(name, roleAttr.Roles);
            }

            var pars = new List<object>();
            var index = 0;

            if (method == null) throw new ArgumentNullException("Method " + name + " not found on " + this.GetType().Name + " view model or are not instance public method");

            foreach (var p in method.GetParameters())
            {
                if(p.ParameterType == typeof(HttpPostedFile))
                {
                    pars.Add(files.FirstOrDefault());
                }
                else if (p.ParameterType == typeof(List<HttpPostedFile>))
                {
                    pars.Add(new List<HttpPostedFile>(files));
                }
                else
                {
                    var token = parameters[index++];

                    if (token.Type == JTokenType.Object)
                    {
                        var obj = ((JObject)token).ToObject(p.ParameterType);

                        pars.Add(obj);
                    }
                    else if(token.Type == JTokenType.String && p.ParameterType.IsEnum)
                    {
                        var value = ((JValue)token).Value.ToString();

                        pars.Add(Enum.Parse(p.ParameterType, value));
                    }
                    else
                    {
                        var value = ((JValue)token).Value;

                        pars.Add(Convert.ChangeType(value, p.ParameterType));
                    }
                }
            }

            OnExecuteMethod(method, pars.ToArray());
        }

        /// <summary>
        /// Call viewmodel method using paramters from client
        /// </summary>
        protected virtual void OnExecuteMethod(MethodInfo method, object[] args)
        {
            method.Invoke(this, args);
        }

        /// <summary>
        /// Test if current user contains role permission
        /// </summary>
        private void AuthorizeMethod(string method, string[] roles)
        {
            var user = Context.User;

            if (user == null)
            {
                throw new UnauthorizedAccessException("Access denied on " + method + " method");
            }

            var accessDenied = true;

            foreach (var role in roles)
            {
                if (Context.User.IsInRole(role))
                {
                    accessDenied = false;
                    break;
                }
            }

            if (accessDenied)
            {
                throw new UnauthorizedAccessException("Access denied on " + method + " method");
            }
        }

        private string RenderUpdate(string model)
        {
            var original = JObject.Parse(model);
            var current = JObject.FromObject(this, new JsonSerializer { ContractResolver = VueContractResolver.Instance });
            var diff = new JObject();

            foreach (var item in current)
            {
                var o = original[item.Key];

                if (original[item.Key] == null && item.Value.HasValues == false) continue;

                if (!JToken.DeepEquals(original[item.Key], item.Value))
                {
                    diff[item.Key] = item.Value;
                }
            }

            var output = new JObject
            {
                { "update", diff },
                { "js", this.JS.ToString() }
            };

            return output.ToString();
        }

        #endregion

        public virtual void Dispose()
        {
        }
    }
}
