using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace EveryoneGetsOne
{
    /// <summary>
    /// Client-local visual hiding for a pickup that the local player has
    /// already collected.  The pickup keeps existing on the server for the
    /// players who still need it; only this client stops drawing it, stops
    /// colliding with it, and stops showing position indicators for it.
    /// </summary>
    internal sealed class LocalHide : MonoBehaviour
    {
        private bool hidden;
        private float nextApplyTime;
        private float nextIndicatorScanTime;
        private float indicatorScanUntil;

        internal bool IsHidden
        {
            get { return hidden; }
        }

        internal static void Hide(GameObject obj)
        {
            if (!obj)
            {
                return;
            }

            LocalHide hide = obj.GetComponent<LocalHide>();
            if (!hide)
            {
                hide = obj.AddComponent<LocalHide>();
            }

            hide.hidden = true;
            hide.indicatorScanUntil = Time.unscaledTime + 15f;
            hide.nextIndicatorScanTime = 0f;
            hide.ApplyNow();
        }

        internal static bool IsObjectHidden(GameObject obj)
        {
            if (!obj)
            {
                return false;
            }

            LocalHide hide = obj.GetComponent<LocalHide>();
            return hide != null && hide.hidden;
        }

        private void LateUpdate()
        {
            if (!hidden)
            {
                return;
            }

            // PickupDisplay can load its model asynchronously, so keep
            // re-applying for a while instead of hiding only once.
            if (Time.unscaledTime < nextApplyTime)
            {
                return;
            }
            nextApplyTime = Time.unscaledTime + 0.5f;
            ApplyNow();
        }

        private void ApplyNow()
        {
            if (!hidden)
            {
                return;
            }

            PickupDisplay[] displays = GetComponentsInChildren<PickupDisplay>(true);
            for (int i = 0; i < displays.Length; i++)
            {
                displays[i].enabled = false;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }

            // On a listen server the same GameObject is also the server-side
            // pickup.  Disabling its colliders would stop other players from
            // walking over it, so only do that on a pure client.
            if (!NetworkServer.active)
            {
                Collider[] colliders = GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    colliders[i].enabled = false;
                }
            }

            Light[] lights = GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].enabled = false;
            }

            AudioSource[] audioSources = GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
            {
                audioSources[i].enabled = false;
            }

            Highlight[] highlights = GetComponentsInChildren<Highlight>(true);
            for (int i = 0; i < highlights.Length; i++)
            {
                highlights[i].enabled = false;
            }

            // Existing radar / ping icons are separate GameObjects.  Remove
            // the ones pointing at this pickup.  New ones are blocked by the
            // PositionIndicator.Start patch and by ShouldShowOnScanner.
            if (Time.unscaledTime < indicatorScanUntil &&
                Time.unscaledTime >= nextIndicatorScanTime)
            {
                nextIndicatorScanTime = Time.unscaledTime + 0.5f;
                PositionIndicator[] indicators = Object.FindObjectsOfType<PositionIndicator>();
                for (int i = 0; i < indicators.Length; i++)
                {
                    PositionIndicator indicator = indicators[i];
                    if (!indicator || !indicator.targetTransform)
                    {
                        continue;
                    }
                    if (indicator.targetTransform.GetComponentInParent<LocalHide>() != this)
                    {
                        continue;
                    }
                    // Ping indicators own a PositionIndicator child; destroying
                    // only the child can leave the ping code with a dead
                    // reference.  The PingIndicator loop below removes the
                    // whole ping instead.
                    if (indicator.GetComponentInParent<PingIndicator>() != null)
                    {
                        continue;
                    }

                    Object.Destroy(indicator.gameObject);
                }

                PingIndicator[] pings = Object.FindObjectsOfType<PingIndicator>();
                for (int i = 0; i < pings.Length; i++)
                {
                    PingIndicator ping = pings[i];
                    if (ping && ping.pingTarget &&
                        ping.pingTarget.transform.GetComponentInParent<LocalHide>() == this)
                    {
                        Object.Destroy(ping.gameObject);
                    }
                }
            }
        }
    }
}
