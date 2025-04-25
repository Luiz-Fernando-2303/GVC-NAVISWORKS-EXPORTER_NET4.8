using Autodesk.Navisworks.Api.Plugins;
using System.Windows.Forms;
using System.Threading;

namespace GVC_EXPORTER_PLUGIN
{
    public static class SharedContext
    {
        public static UI_Components.ProgressMonitor monitorInstance;
        public static Thread monitorThread;
    }

    [Plugin("SetZones", "ADSK", DisplayName = "Set zones", ToolTip = "")]
    public class SetZones : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            if (SharedContext.monitorThread == null || !SharedContext.monitorThread.IsAlive)
            {
                SharedContext.monitorThread = new Thread(() =>
                {
                    SharedContext.monitorInstance = new UI_Components.ProgressMonitor();
                    Application.Run(SharedContext.monitorInstance);
                });

                SharedContext.monitorThread.SetApartmentState(ApartmentState.STA);
                SharedContext.monitorThread.IsBackground = true;
                SharedContext.monitorThread.Start();
            }

            double correction = 19.0;
            ContextInitializer.InitializeContext(correction);

            return 0;
        }
    }
}
