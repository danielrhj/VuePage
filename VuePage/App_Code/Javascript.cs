﻿using System.Text;

namespace Vue
{
    public class Javascript
    {
        private StringBuilder _sb = new StringBuilder();

        public int Length => _sb.Length;

        public Javascript ConsoleLog(string text)
        {
            return Code("console.log('{0}');", text);
        }

        public Javascript Alert(string text)
        {
            return Code("alert('{0}');", text);
        }

        public Javascript Focus(string id)
        {
            return Code("try {{ var f = document.querySelector('.vue-page-active #{0}'); if (f) {{ f.focus(); }} }} catch(e) {{ }}", id);
        }

        public Javascript NavigateTo(string url)
        {
            return Code("navToPage('{0}');", url);
        }

        public Javascript RedirectTo(string url)
        {
            return Code("location.href = '{0}';", url);
        }

        public Javascript Code(string code)
        {
            _sb.Append(code);
            return this;
        }

        public Javascript Code(string format, params object[] args)
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