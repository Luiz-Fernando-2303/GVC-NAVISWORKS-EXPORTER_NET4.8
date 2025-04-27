using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using GVC_EXPORTER_PLUGIN.Functions;
using GVC_EXPORTER_PLUGIN.Functions.Clash_;
using GVC_EXPORTER_PLUGIN.Functions.ModelItemBoxCreation;

namespace GVC_EXPORTER_PLUGIN
{
    internal class Context
    {
        private static Context _instance;

        public static Context Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Context();
                }
                return _instance;
            }
        }

        public Context()
        {
            Clear();
        }

        // Configurações
        public double RotationCorrection { get; set; }

        // Dados principais
        public ModelItemCollection _items { get; set; }
        public List<BoxedModelitem> _points { get; set; }
        public List<BoxedModelitem> _ZonePoints { get; set; }

        // Estado geral para UI
        public Dictionary<string, (string name, int size, int count)> _state { get; set; }

        public void Clear()
        {
            RotationCorrection = 0.0;
            _items = new ModelItemCollection();
            _points = new List<BoxedModelitem>(2_000_000);
            _ZonePoints = new List<BoxedModelitem>();
            _state = new Dictionary<string, (string, int, int)>();
        }
    }

    public static class ContextInitializer
    {
        private class ContextBackup
        {
            public ModelItemCollection Items { get; set; }
            public List<BoxedModelitem> Points { get; set; }
            public List<BoxedModelitem> ZonePoints { get; set; }
            public Dictionary<string, (string name, int size, int count)> State { get; set; }
        }

        public static void InitializeContext(double angleCorrection = 0.0)
        {
            var backup = new ContextBackup
            {
                Items = Context.Instance._items,
                Points = Context.Instance._points,
                ZonePoints = Context.Instance._ZonePoints,
                State = Context.Instance._state
            };

            try
            {
                Context.Instance.Clear();
                Context.Instance.RotationCorrection = angleCorrection;

                Context.Instance._state["state"] = ("Juntando ModelItems", 0, 0);
                var models = Application.ActiveDocument.Models;


                var ModelItemsAccumulator = new List<ModelItem>(2_000_000);
                ModelItemBoxCreation.RecursivePackPoints(models.RootItems, Context.Instance._points, ModelItemsAccumulator);
                Context.Instance._items.AddRange(ModelItemsAccumulator);

                Context.Instance._ZonePoints = ModelItemBoxCreation.GetPackedPoints(
                    Application.ActiveDocument.CurrentSelection.SelectedItems
                );

                Clash_Functions.RunZonesClash(
                    "Zone Detection",
                    Context.Instance._ZonePoints,
                    Context.Instance._points
                );

                Zone_Detection.AddZonesToitems();

            } catch
            {
                RestoreBackup(backup);
            }
        }

        private static void RestoreBackup(ContextBackup backup)
        {
            Context.Instance._items = backup.Items;
            Context.Instance._points = backup.Points;
            Context.Instance._ZonePoints = backup.ZonePoints;
            Context.Instance._state = backup.State;
        }
    }
}
