﻿using System.Text;
using System.Web;
using Newtonsoft.Json;

namespace Vue
{
    /// <summary>
    /// Javascript builder class
    /// </summary>
    public class JavascriptBuilder
    {
        private StringBuilder _sb = new StringBuilder();

        public int Length => _sb.Length;

        public JavascriptBuilder ConsoleLog(string text)
        {
            return Code("console.log('{0}');", HttpUtility.JavaScriptStringEncode(text));
        }

        public JavascriptBuilder Alert(string text)
        {
            return Code("alert('{0}');", HttpUtility.JavaScriptStringEncode(text));
        }

        public JavascriptBuilder Focus(string id)
        {
            return Code("try {{ var f = document.querySelector('.vue-page-active #{0}'); if (f) {{ f.focus(); }} }} catch(e) {{ }}", id);
        }

        public JavascriptBuilder NavigateTo(string url)
        {
            return Code("this.$navigate('{0}');", HttpUtility.JavaScriptStringEncode(url));
        }

        public JavascriptBuilder RedirectTo(string url)
        {
            return Code("location.href = '{0}';", HttpUtility.JavaScriptStringEncode(url));
        }

        public JavascriptBuilder Emit(string evnt, params object[] args)
        {
            var sb = new StringBuilder("this.$emit('" + evnt +"'");

            foreach(var arg in args)
            {
                sb.Append(", " + JsonConvert.SerializeObject(arg));
            }

            sb.Append(");");

            return Code(sb.ToString());
        }

        public JavascriptBuilder Code(string code)
        {
            _sb.Append(code);
            return this;
        }

        public JavascriptBuilder Code(string format, params object[] args)
        {
            _sb.AppendFormat(format, args);
            return this;
        }

        public override string ToString()
        {
            return _sb.ToString();
        }
    }
}
