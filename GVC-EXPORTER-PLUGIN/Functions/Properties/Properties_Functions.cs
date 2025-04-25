using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;

namespace GVC_EXPORTER_PLUGIN.Functions.Properties
{
    public static class Properties_Functions
    {
        private static readonly InwOpState3 state = ComApiBridge.State;

        public static void AddPropertiesToModelItem(ModelItem model, string category, Dictionary<string, string> properties)
        {
            InwOpState3 oState = state;
            InwOaPath oaPath = ComApiBridge.ToInwOaPath(model);

            InwGUIPropertyNode2 propVec = (InwGUIPropertyNode2)oState.GetGUIPropertyNode(oaPath, true);

            InwOaPropertyVec newPropVec = (InwOaPropertyVec)oState.ObjectFactory(nwEObjectType.eObjectType_nwOaPropertyVec, null, null);

            foreach (var kvp in properties)
            {
                InwOaProperty newProp = (InwOaProperty)oState.ObjectFactory(nwEObjectType.eObjectType_nwOaProperty, null, null);

                newProp.name = kvp.Key;
                newProp.UserName = kvp.Key;
                newProp.value = kvp.Value;

                newPropVec.Properties().Add(newProp);
            }

            propVec.SetUserDefined(0, category, category, newPropVec);
            GC.KeepAlive(propVec);

            System.Windows.Forms.Application.DoEvents();
        }
    }
}
