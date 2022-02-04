using System;
using System.Collections;
using UnityEngine;

namespace BetterSongSearch.Util
{
    internal class RatelimitCoroutine
    {
        private readonly Action exitfn;
        private readonly float limit;
        public RatelimitCoroutine(Action exitfn, float limit = 0.5f)
        {
            this.exitfn = exitfn;
            this.limit = limit;
        }

        private bool wasRecentlyExecuted = false;
        private bool queuedFallingEdge = false;

        public IEnumerator Call()
        {
            if (!wasRecentlyExecuted)
            {
                wasRecentlyExecuted = true;
                yield return CallNow();
            }
            else
            {
                queuedFallingEdge = true;
            }
        }

        public IEnumerator CallNextFrame()
        {
            yield return null;
            yield return Call();
        }

        private IEnumerator CallNow()
        {
            exitfn();

            yield return new WaitForSeconds(limit);

            if (queuedFallingEdge)
            {
                queuedFallingEdge = false;
                yield return CallNow();
            }
            else
            {
                wasRecentlyExecuted = false;
            }
        }
    }
}
