using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Equilibrium.Tests.PlayMode
{
    /// <summary>
    /// Smoke test proving the PlayMode test assembly compiles and runs.
    /// </summary>
    public class FrameworkSmokeTest
    {
        [UnityTest]
        public IEnumerator test_playmode_frame_advances_expected_time_increases()
        {
            float start = Time.time;
            yield return null;
            Assert.GreaterOrEqual(Time.time, start);
        }
    }
}
