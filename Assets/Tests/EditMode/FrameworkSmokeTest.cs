using NUnit.Framework;

namespace Equilibrium.Tests.EditMode
{
    /// <summary>
    /// Smoke test proving the EditMode test assembly compiles and runs.
    /// Real tests are added per story against GDD acceptance criteria
    /// (see docs/architecture/control-manifest.md, Testing section).
    /// </summary>
    public class FrameworkSmokeTest
    {
        [Test]
        public void test_framework_runs_expected_true()
        {
            Assert.IsTrue(true);
        }

        [Test]
        public void test_effective_capacity_formula_expected_min()
        {
            // TR-cont-001: effectiveCapacity = min(slot.capacity, itemDef.maxStack)
            const int slotCapacity = 3000;
            const int maxStack = 64;
            Assert.AreEqual(64, System.Math.Min(slotCapacity, maxStack));
        }
    }
}
