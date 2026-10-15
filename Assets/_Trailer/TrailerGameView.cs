using System.Reflection;
using UnityEditor;

namespace Bouncer.Trailer
{
    // Game view на фиксированные 1920×1080: клипы с интерфейсом снимаются прямо с него (TrailerRecorder.Hud).
    public static class TrailerGameView
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        public static string Set(int width = 1920, int height = 1080)
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = single.GetProperty("instance", Any).GetValue(null);
            var groupType = sizesType.GetProperty("currentGroupType", Any).GetValue(sizes);
            var group = sizesType.GetMethod("GetGroup", Any).Invoke(sizes, new[] { groupType });
            var gType = group.GetType();
            int total = (int)gType.GetMethod("GetTotalCount", Any).Invoke(group, null);
            var getSize = gType.GetMethod("GetGameViewSize", Any);
            int index = -1;
            for (int i = 0; i < total; i++)
            {
                var s = getSize.Invoke(group, new object[] { i });
                var st = s.GetType();
                if ((int)st.GetProperty("width", Any).GetValue(s) == width && (int)st.GetProperty("height", Any).GetValue(s) == height
                    && st.GetProperty("sizeType", Any).GetValue(s).ToString() == "FixedResolution")
                    index = i;
            }
            if (index < 0)
            {
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var kind = asm.GetType("UnityEditor.GameViewSizeType");
                var size = System.Activator.CreateInstance(sizeType, System.Enum.Parse(kind, "FixedResolution"), width, height, "Trailer");
                gType.GetMethod("AddCustomSize", Any).Invoke(group, new[] { size });
                index = total;
            }
            var gvType = asm.GetType("UnityEditor.GameView");
            var gv = EditorWindow.GetWindow(gvType, false, null, false);
            var prop = gvType.GetProperty("selectedSizeIndex", Any);
            if (prop != null)
                prop.SetValue(gv, index);
            else
                gvType.GetMethod("SizeSelectionCallback", Any).Invoke(gv, new object[] { index, null });
            gv.Repaint();
            return "game view size index " + index;
        }
    }
}
