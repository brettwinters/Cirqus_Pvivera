using System;

namespace d60.Cirqus.Events
{
    public class DisplayAttribute : Attribute
    {
        public DisplayAttribute(string template)
        {
            Template = template;
        }

        public string Template { get; private set; }
    }

}