#if !EQ_PURE
using Equilibrium.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Equilibrium.Tests.EditMode.Logistics
{
    public sealed class LogisticsWorkbenchTests
    {
        [Test]
        public void WorkbenchRunsRealLineAndInspectionReadsCommittedState()
        {
            var window = ScriptableObject.CreateInstance<LogisticsWorkbench>();
            try
            {
                window.CreateGUI();
                Assert.IsNotNull(window.rootVisualElement.Q<Label>("logistics-summary"));
                Assert.AreEqual(128, window.CountAt(0));
                window.SetBlocked(true);
                for (int i = 0; i < 12; i++) window.AdvanceOneTick();
                window.SelectCell(2);
                StringAssert.Contains("堵塞", window.InspectionText);
                Assert.AreEqual(0, window.CountAt(5));
                window.SetBlocked(false);
                for (int i = 0; i < 30; i++) window.AdvanceOneTick();
                Assert.AreEqual(128, window.CountAt(5)); Assert.AreEqual(0, window.CountAt(0));
                window.CollectOutput(); Assert.AreEqual(0, window.CountAt(5));
                StringAssert.Contains("128/128", window.rootVisualElement.Q<Label>("logistics-summary").text);
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
#endif
