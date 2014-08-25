using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HelloRust
{
    public static class ControlHelpers
    {
        public static void InvokeIfRequired(this Control control, MethodInvoker action)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(action);
            }
            else
            {
                action();
            }
        }

        public static void AppendLine(this TextBox source, string value)
        {
            source.AppendText(value + Environment.NewLine);
        }
    }
}
