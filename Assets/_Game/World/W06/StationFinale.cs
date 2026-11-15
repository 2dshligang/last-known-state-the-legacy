using System;
using UnityEngine;

namespace LKS.World
{
    [DisallowMultipleComponent]
    public sealed partial class StationFinale : MonoBehaviour, INarrativeView
    {
        private enum Page { Closed, Photograph, Credits, Menu }
        private StationFirstPerson player;
        private StationInteraction station;
        private GameplaySession observed;
        private IDisposable lease;
        private IDisposable completion;
        private bool handledCompletion;
        private Page page;
        private int focus;
        private Texture2D photograph;
        private string status;

        internal bool IsOpen => lease != null;
        internal string FinalText { get; private set; }
        internal bool InCredits => page == Page.Credits;
        private bool Completed => observed?.Ending?.Completion.Snapshot == true;
        private bool CanControl => IsOpen && station.isActiveAndEnabled && player.HasApplicationFocus && !player.IsPaused &&
            ReferenceEquals(observed, station.Session);

        internal void Bind(StationFirstPerson input, StationInteraction bridge)
        {
            player = input;
            station = bridge;
        }

        private void Update() => ObserveSession();
        private void OnDisable()
        {
            Close();
            completion?.Dispose();
            completion = null;
            observed = null;
        }

        internal string Prompt()
        {
            var session = station.Session;
            if (session?.Ending == null) return "The photograph is unavailable.";
            if (session.Ending.TryGetFinalCue(out _)) return "E  " + Resolve("ending.action.replay");
            var readiness = session.Ending.Readiness();
            return readiness.Code == ResultCode.Applied ? "E  Inspect the photograph\nSurface overlook" :
                "Surface overlook\n" + Resolve(readiness.MessageKey);
        }

        internal bool Inspect(out string feedback)
        {
            feedback = null;
            ObserveSession();
            if (observed?.Ending == null || player.HasReadingFocus || player.IsPaused || !player.HasApplicationFocus)
                return false;
            if (observed.Ending.TryGetFinalCue(out _)) return Replay();
            if (!observed.TryGetTarget(StationEnding.Photograph, out var target)) return false;
            var state = target.Read(StationQuests.Player);
            if (!state.Available)
            {
                feedback = Resolve(state.PromptKey);
                return false;
            }
            var result = target.Request(StationQuests.Player, StationQuests.Confirm);
            if (!result.Succeeded)
            {
                feedback = Resolve(result.MessageKey);
                return false;
            }
            CommitAndPresent();
            return IsOpen;
        }

        private void ObserveSession()
        {
            var session = station != null && station.isActiveAndEnabled ? station.Session : null;
            if (ReferenceEquals(session, observed)) return;
            Close();
            completion?.Dispose();
            observed = session;
            handledCompletion = Completed;
            status = null;
            completion = observed?.Ending?.Completion.Subscribe(change =>
            {
                if (ReferenceEquals(observed, station.Session) && change.Subject == StationEnding.Photograph &&
                    change.Cause == StationEnding.PhotographInspected && change.Snapshot)
                    CommitAndPresent();
            });
        }

        private void CommitAndPresent()
        {
            if (handledCompletion) return;
            handledCompletion = true;
            var result = station.SaveCampaign();
            status = result.Succeeded ? null : Resolve(result.MessageKey);
            Replay();
        }

        internal bool Replay()
        {
            if (observed?.Ending == null || !observed.Ending.TryGetFinalCue(out var cue) || !Show(Page.Photograph))
                return false;
            cue.Present(this, observed.Archive.Content);
            return true;
        }

        void INarrativeView.ShowText(string text, StableId portrait, StableId cameraCue)
        {
            FinalText = text;
            if (portrait.value == "portrait.dmitry-photograph" && cameraCue.value == "camera.final-photograph")
                photograph ??= Resources.Load<Texture2D>("W14/DmitryPhoto");
        }

        internal void OpenMenu()
        {
            ObserveSession();
            if (Show(Page.Menu)) player.SetPaused(false);
        }

        internal bool Credits()
        {
            if (!CanControl || page != Page.Photograph) return false;
            return Show(Page.Credits);
        }

        internal bool Continue()
        {
            if (!CanControl || page != Page.Menu) return false;
            var result = station.LoadCampaign();
            if (!result.Succeeded)
            {
                status = Resolve(result.MessageKey);
                return false;
            }
            ObserveSession();
            if (Completed) Replay();
            return true;
        }

        internal bool SkipToMenu()
        {
            if (!CanControl || (page != Page.Photograph && page != Page.Credits)) return false;
            return Show(Page.Menu);
        }

        private bool Show(Page next)
        {
            if (!isActiveAndEnabled || player == null || !player.isActiveAndEnabled) return false;
            if (lease == null)
            {
                if (player.HasReadingFocus) return false;
                lease = player.AcquireReadingFocus(Close);
            }
            page = next;
            focus = 0;
            return true;
        }

        internal void Close()
        {
            var prior = lease;
            lease = null;
            page = Page.Closed;
            FinalText = null;
            prior?.Dispose();
        }

        private void ReturnToStation()
        {
            Close();
            if (!player.HasWorldFocus) player.ResumePointer();
        }

        private string Resolve(string key) => Resolve(new StableId(key));
        private string Resolve(StableId key) => station.Session.Archive.Content.Resolve(key, false);
    }
}
