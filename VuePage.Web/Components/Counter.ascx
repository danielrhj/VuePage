﻿<%@ Control Language="C#" ClassName="Counter" %>
<script runat="server">

    public class CounterVM : ViewModel
    {
        public int Counter { get; set; }

        public CounterVM(HttpContext ctx)
        {
            this.Created += (s, e) =>
            {
                Counter = DateTime.Now.Second;
                //JS.ConsoleLog("HttpContext works here? " + (ctx != null));
                JS.Code("this.$el.classList.remove('hidden');");
            };
        }

        public void Increment()
        {
            Counter++;
            JS.Emit("inc");
        }
    }

</script>
<button @click="Increment()" class="hidden">{{ Counter }}</button>
