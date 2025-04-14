using GVC_EXPORTER_PLUGIN.Plugins.Render;
using Autodesk.Navisworks.Api.Plugins;
using GVC_EXPORTER_PLUGIN.Plugins.Ui;

namespace GVC_EXPORTER_PLUGIN
{

    [Plugin("RenderModule", "ADSK", DisplayName = "RenderModule", ToolTip = "RenderModule")]
    public class RenderModule : RenderOnScene { }

    [Plugin("Chunk manager", "ADSK", DisplayName = "Chunk manager", ToolTip = "")]
    [DockPanePlugin(800,600, AutoScroll = true, FixedSize = false)]
    public class UiPanel : ChunkManagerDockPane { }
}
