using System;

namespace Attributes.Source.Infrastructure.Inspector
{
    public class ButtonAttribute : Attribute
    {
        public string Label { get; }

        public ButtonAttribute(string label = null)
        {
            Label = label;
        }
    }
}