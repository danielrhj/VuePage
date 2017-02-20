﻿<%@ Control Language="C#" ClassName="Counter" %>
<script runat="server">

    public class CounterVM : Vue.ViewModel<CounterVM>
    {
        public int Counter { get; set; }

        public CounterVM(HttpContext ctx)
        {
            Created(() =>
            {
                JS.ConsoleLog("HttpContext works here? " + (ctx != null));
                Counter = DateTime.Now.Second;
                JS.Code("this.$el.classList.remove('hidden');");
            });
        }

        public void Increment()
        {
            Counter++;
            JS.Emit("inc");
        }
    }

</script>
<button @click="Increment()" class="hidden">{{ Counter }}</button>
