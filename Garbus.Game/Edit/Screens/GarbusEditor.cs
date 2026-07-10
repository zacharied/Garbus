// Editor shell: tabs, menu bar, hotkeys, dirty tracking, DI caching.

using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Format;
using Garbus.Game.Configuration;
using Garbus.Game.Edit.Screens.BottomBar;
using Garbus.Game.Edit.Screens.Dialogs;
using Garbus.Game.Screens;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osuTK.Input;

namespace Garbus.Game.Edit.Screens
{
    public partial class GarbusEditor : Screen
    {
        // --- Public contract ---

        public readonly Bindable<EditorTab> Tab = new Bindable<EditorTab>(EditorTab.Compose);

        public ChartFile ChartFile { get; }

        public bool HasUnsavedChanges => changeHandler.CurrentStateHash != hashAtLastSave;

        public EditorChart EditorChart { get; private set; } = null!;

        /// <summary>
        /// Whether the editor currently has a real (non-virtual) track loaded.
        /// Bound to the Test button's enabled state.
        /// </summary>
        public readonly Bindable<bool> TrackIsReal = new Bindable<bool>(false);

        /// <summary>Convenience accessor for the current <see cref="TrackIsReal"/> value.</summary>
        public bool HasRealTrack => TrackIsReal.Value;

        // --- Internal test seam ---

        private Func<Track>? trackFactoryOverride;

        /// <summary>
        /// When set, this factory is called instead of <see cref="ChartFile.GetTrackStore"/> to
        /// obtain a fresh track for test mode. Intended for headless tests only — in production this
        /// is always null and the directory store is used.
        /// Setting this also forces <see cref="TrackIsReal"/> to true so the Test button is enabled.
        /// </summary>
        internal Func<Track>? TrackFactoryOverride
        {
            get => trackFactoryOverride;
            set
            {
                trackFactoryOverride = value;
                if (value != null)
                    TrackIsReal.Value = true;
            }
        }

        // --- Private state ---

        private EditorClock editorClock = null!;
        private GarbusChartChangeHandler changeHandler = null!;

        /// <summary>Exposed for tests that need to assert undo/redo state directly.</summary>
        internal GarbusChartChangeHandler ChangeHandlerForTests => changeHandler;
        private BindableBeatDivisor beatDivisor = null!;
        private AudioManager audioManager = null!;
        private EditorClipboard clipboard = null!;

        private string hashAtLastSave = string.Empty;

        /// <summary>
        /// Set to true once the user has confirmed a discard-and-exit so that
        /// <see cref="OnExiting"/> does not re-show the save dialog on the second
        /// call that the framework issues after we call <see cref="IScreen.Exit"/>.
        /// </summary>
        private bool exitConfirmed;

        private DependencyContainer dependencies = null!;

        private ComposeTab composeTab = null!;
        private SetupTab setupTab = null!;
        private TimingTab timingTab = null!;
        private VerifyTab verifyTab = null!;

        private Container tabContainer = null!;
        private Container dialogOverlay = null!;

        public GarbusEditor(ChartFile chartFile)
        {
            ChartFile = chartFile;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            // Build the editor object graph and cache into DI.
            EditorChart = new EditorChart(ChartFile.Chart);
            changeHandler = new GarbusChartChangeHandler(EditorChart);

            beatDivisor = new BindableBeatDivisor(4);

            // hashAtLastSave: captured after GarbusChartChangeHandler has saved its first snapshot.
            hashAtLastSave = changeHandler.CurrentStateHash;

            editorClock = new EditorClock(EditorChart.ControlPointInfo, 60000, beatDivisor);

            clipboard = new EditorClipboard(EditorChart, editorClock, EditorChart.ControlPointInfo, beatDivisor);

            dependencies.Cache(editorClock);
            dependencies.Cache(EditorChart);
            dependencies.Cache(changeHandler);
            dependencies.CacheAs<IEditorChangeHandler>(changeHandler);
            dependencies.Cache(beatDivisor);
            dependencies.Cache(clipboard);
            dependencies.CacheAs(this);
            dependencies.CacheAs(ChartFile);
            // Cache ControlPointInfo directly so timeline components (TimelineTickDisplay,
            // TimelineTimingChangeDisplay) can resolve it without going through EditorChart.
            dependencies.CacheAs(EditorChart.ControlPointInfo);

            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            audioManager = audio;
            RelativeSizeAxes = Axes.Both;

            // Load track.
            ReloadTrack(audioManager);

            // Build layout.
            // Use a plain Container with Padding rather than a FillFlowContainer:
            // a child with RelativeSizeAxes.Both inside a vertical FillFlowContainer resolves to
            // zero height — the tab area collapses.  Padding reserves the top/bottom bar heights.
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new osuTK.Graphics.Color4(28, 28, 36, 255),
                },
                createTopBar(),
                tabContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = 40, Bottom = 60 },
                },
                createBottomBar(),
                dialogOverlay = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
            };

            // Tab content — stacked, only one visible.
            tabContainer.Children = new Drawable[]
            {
                setupTab = new SetupTab { RelativeSizeAxes = Axes.Both, State = { Value = Visibility.Hidden } },
                composeTab = new ComposeTab { RelativeSizeAxes = Axes.Both, State = { Value = Visibility.Hidden } },
                timingTab = new TimingTab { RelativeSizeAxes = Axes.Both, State = { Value = Visibility.Hidden } },
                verifyTab = new VerifyTab { RelativeSizeAxes = Axes.Both, State = { Value = Visibility.Hidden } },
            };

            // EditorClock must be in the hierarchy to process.
            AddInternal(editorClock);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Tab.BindValueChanged(e => updateTabVisibility(e.NewValue), true);
        }

        // --- Tab management ---

        private void updateTabVisibility(EditorTab activeTab)
        {
            setupTab.State.Value = activeTab == EditorTab.Setup ? Visibility.Visible : Visibility.Hidden;
            composeTab.State.Value = activeTab == EditorTab.Compose ? Visibility.Visible : Visibility.Hidden;
            timingTab.State.Value = activeTab == EditorTab.Timing ? Visibility.Visible : Visibility.Hidden;
            verifyTab.State.Value = activeTab == EditorTab.Verify ? Visibility.Visible : Visibility.Hidden;
        }

        // --- Save / SaveAs ---

        public void Save()
        {
            if (ChartFile.FilePath == null)
            {
                SaveAs();
                return;
            }

            ChartFile.Save();
            hashAtLastSave = changeHandler.CurrentStateHash;
        }

        public void SaveAs()
        {
            var dialog = new SaveAsDialog(path =>
            {
                ChartFile.Save(path);
                hashAtLastSave = changeHandler.CurrentStateHash;
            }, defaultFilename: string.IsNullOrEmpty(ChartFile.Chart.Metadata.Title) ? "new-chart" : ChartFile.Chart.Metadata.Title);

            dialogOverlay.Child = dialog;
            dialog.Show();
        }

        // --- Track loading ---

        public void ReloadTrack() => ReloadTrack(null);

        private void ReloadTrack(AudioManager? audioManager)
        {
            Track track;
            var store = ChartFile.GetTrackStore(audioManager ?? dependencies.Get<AudioManager>());

            if (store != null && !string.IsNullOrEmpty(ChartFile.Chart.Metadata.AudioFile))
            {
                track = store.Get(ChartFile.Chart.Metadata.AudioFile) ?? new TrackVirtual(60000);
            }
            else
            {
                track = new TrackVirtual(60000);
            }

            // Update the TrackIsReal bindable: a real track is anything that is NOT a TrackVirtual,
            // OR when a test factory override is set (which always provides a headless "real" track).
            // This controls whether the Test button is enabled.
            TrackIsReal.Value = track is not TrackVirtual || trackFactoryOverride != null;

            editorClock.ChangeSource(track);
        }

        // --- Test mode ---

        /// <summary>
        /// Launches a <see cref="PlayScreen"/> with a deep clone of the current chart, starting
        /// 1500 ms before the current editor position. Called by the Test button and F5.
        /// When <see cref="TrackFactoryOverride"/> is set (headless tests only), that factory is
        /// used to create the track instead of the chart directory store.
        /// </summary>
        public void StartTestMode()
        {
            // Determine which track to use.
            Track? freshTrack = null;

            if (trackFactoryOverride != null)
            {
                // Test seam: headless tests inject a factory so the push can happen without a real
                // audio file on disk.
                freshTrack = trackFactoryOverride();
            }
            else
            {
                // Production path: obtain a NEW track instance from the chart directory store.
                // We must NOT share the editor's own track instance with the gameplay clock.
                var store = ChartFile.GetTrackStore(audioManager);
                if (store != null && !string.IsNullOrEmpty(ChartFile.Chart.Metadata.AudioFile))
                    freshTrack = store.Get(ChartFile.Chart.Metadata.AudioFile);
            }

            if (freshTrack == null)
                return; // No track available; button should have been disabled.

            // Deep-clone the chart via the serializer (ensures zero shared references).
            var clonedChart = GarbusChartSerializer.Decode(GarbusChartSerializer.Encode(EditorChart.Chart));
            clonedChart.ApplyDefaults();

            // Start 1500 ms before the editor's current position, clamped to ≥ 0.
            double startTime = Math.Max(0, editorClock.CurrentTime - 1500);

            this.Push(new PlayScreen(clonedChart, freshTrack, startTime));
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);

            // If returning from a PlayScreen, seek the editor clock to where gameplay ended.
            if (e.Last is PlayScreen playScreen && playScreen.ExitTime.HasValue)
                editorClock.Seek(playScreen.ExitTime.Value);
        }

        // --- Layout helpers ---

        private Drawable createTopBar()
        {
            var bar = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 40,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new osuTK.Graphics.Color4(20, 20, 28, 255),
                    },
                    createMenuBar(),
                    new BasicTabControl<EditorTab>
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        RelativeSizeAxes = Axes.Y,
                        Width = 320,
                        Items = Enum.GetValues<EditorTab>(),
                        Current = Tab,
                    },
                },
            };

            return bar;
        }

        private Drawable createMenuBar()
        {
            return new BasicMenu(Direction.Horizontal, true)
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                RelativeSizeAxes = Axes.Y,
                Items = new[]
                {
                    new MenuItem("File")
                    {
                        Items = createFileMenuItems(),
                    },
                    new MenuItem("Edit")
                    {
                        Items = createEditMenuItems(),
                    },
                    new MenuItem("View")
                    {
                        Items = createViewMenuItems(),
                    },
                    new MenuItem("Timing")
                    {
                        Items = createTimingMenuItems(),
                    },
                },
            };
        }

        private IReadOnlyList<MenuItem> createViewMenuItems()
        {
            // Resolve config from DI. The editor shell is loaded under GarbusGameBase which caches it.
            var config = dependencies.Get<GarbusConfigManager>();

            var showTicks = config.GetBindable<bool>(GarbusSetting.EditorShowTicks);
            var showTimingChanges = config.GetBindable<bool>(GarbusSetting.EditorShowTimingChanges);
            var autoSeek = config.GetBindable<bool>(GarbusSetting.EditorAutoSeekOnPlacement);
            var contractSidebars = config.GetBindable<bool>(GarbusSetting.EditorContractSidebars);

            return new[]
            {
                new MenuItem("Show Beat Ticks", () => showTicks.Value = !showTicks.Value),
                new MenuItem("Show Timing Changes", () => showTimingChanges.Value = !showTimingChanges.Value),
                new MenuItem("Auto-Seek on Placement", () => autoSeek.Value = !autoSeek.Value),
                new MenuItem("Contract Sidebars", () => contractSidebars.Value = !contractSidebars.Value),
            };
        }

        private IReadOnlyList<MenuItem> createFileMenuItems() => new[]
        {
            new MenuItem("New", () =>
            {
                void doNew()
                {
                    var chart = new Charts.GarbusChart();
                    chart.ControlPointInfo.Add(0, new Charts.Timing.TimingControlPoint { BeatLength = 500 });
                    this.Push(new GarbusEditor(new Charts.ChartFile(chart)));
                }

                if (HasUnsavedChanges)
                    showConfirmThenRun(doNew);
                else
                    doNew();
            }),
            new MenuItem("Open…", () =>
            {
                void doOpen()
                {
                    var dialog = new OpenChartDialog(path =>
                    {
                        try
                        {
                            var chartFile = Charts.ChartFile.Load(path);
                            this.Push(new GarbusEditor(chartFile));
                        }
                        catch (Exception ex)
                        {
                            dialogOverlay.Child = new ConfirmDialog($"Failed to open chart:\n{ex.Message}", ("OK", () => { }));
                            ((ConfirmDialog)dialogOverlay.Child).Show();
                        }
                    });
                    dialogOverlay.Child = dialog;
                    dialog.Show();
                }

                if (HasUnsavedChanges)
                    showConfirmThenRun(doOpen);
                else
                    doOpen();
            }),
            new MenuItem("Save", Save),
            new MenuItem("Save As…", SaveAs),
            new MenuItem("Exit", this.Exit),
        };

        private IReadOnlyList<MenuItem> createEditMenuItems()
        {
            var undoItem = new MenuItem("Undo", () => changeHandler.Undo());
            var redoItem = new MenuItem("Redo", () => changeHandler.Redo());

            // Bind enabled state. MenuItem.Action being null disables the item in BasicMenu.
            // We achieve enabled/disabled by swapping the action reference.
            changeHandler.CanUndo.BindValueChanged(e =>
                undoItem.Action.Value = e.NewValue ? (Action)changeHandler.Undo : null, true);
            changeHandler.CanRedo.BindValueChanged(e =>
                redoItem.Action.Value = e.NewValue ? (Action)changeHandler.Redo : null, true);

            var cutItem = new MenuItem("Cut", () => clipboard.Cut());
            var copyItem = new MenuItem("Copy", () => clipboard.Copy());
            var pasteItem = new MenuItem("Paste", () => clipboard.Paste());
            var cloneItem = new MenuItem("Clone", () => clipboard.Clone());

            // Update clipboard menu item enabled states when selection or clipboard content changes.
            void updateClipboardEnabled()
            {
                bool hasSelection = EditorChart.SelectedHitObjects.Count > 0;
                bool hasContent = !string.IsNullOrEmpty(clipboard.Content.Value);
                cutItem.Action.Value = hasSelection ? (Action)clipboard.Cut : null;
                copyItem.Action.Value = hasSelection ? (Action)clipboard.Copy : null;
                pasteItem.Action.Value = hasContent ? (Action)clipboard.Paste : null;
                cloneItem.Action.Value = hasSelection ? (Action)clipboard.Clone : null;
            }

            EditorChart.SelectedHitObjects.BindCollectionChanged((_, _) => updateClipboardEnabled(), true);
            clipboard.Content.BindValueChanged(_ => updateClipboardEnabled());

            return new[] { undoItem, redoItem, cutItem, copyItem, pasteItem, cloneItem };
        }

        private IReadOnlyList<MenuItem> createTimingMenuItems()
        {
            return new[]
            {
                new MenuItem("Set preview point to current time", () =>
                {
                    EditorChart.BeginChange();
                    EditorChart.Chart.PreviewTime = editorClock.CurrentTime;
                    EditorChart.SaveState();
                    EditorChart.EndChange();
                }),
                new MenuItem("Snap all notes to current snap divisor", () =>
                {
                    var dialog = new ConfirmDialog(
                        "This will move all notes to the nearest beat grid position.\nThis action is undoable.",
                        ("Snap All", () =>
                        {
                            EditorChart.BeginChange();
                            foreach (var h in EditorChart.HitObjects.ToList())
                            {
                                h.StartTime = EditorChart.ControlPointInfo.GetClosestSnappedTime(h.StartTime, beatDivisor.Value);
                                EditorChart.Update(h);
                            }
                            EditorChart.EndChange();
                        }),
                        ("Cancel", () => { })
                    );
                    dialogOverlay.Child = dialog;
                    dialog.Show();
                }),
            };
        }

        private Drawable createBottomBar() => new BottomBar.BottomBar();

        // --- Screen exit ---

        public override bool OnExiting(ScreenExitEvent e)
        {
            if (HasUnsavedChanges && !exitConfirmed)
            {
                // Show the save/discard/cancel dialog; block the exit (return true).
                showConfirmThenRun(() =>
                {
                    exitConfirmed = true;
                    this.Exit();
                });
                return true;
            }

            return base.OnExiting(e);
        }

        // --- Hotkeys ---

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat)
                return base.OnKeyDown(e);

            // F5: launch test mode (no modifiers required).
            if (e.Key == Key.F5 && !e.ControlPressed && !e.AltPressed && !e.SuperPressed)
            {
                if (TrackIsReal.Value)
                    StartTestMode();
                return true;
            }

            if (e.ControlPressed)
            {
                switch (e.Key)
                {
                    case Key.S:
                        Save();
                        return true;

                    case Key.Z:
                        if (e.ShiftPressed)
                            changeHandler.Redo();
                        else
                            changeHandler.Undo();
                        return true;

                    case Key.Y:
                        changeHandler.Redo();
                        return true;

                    // Clipboard hotkeys — only when no textbox is focused (textbox handles Ctrl+C/V itself).
                    case Key.X:
                        if (!isTextBoxFocused() && clipboard.CanCut)
                        {
                            clipboard.Cut();
                            return true;
                        }
                        break;

                    case Key.C:
                        if (!isTextBoxFocused() && clipboard.CanCopy)
                        {
                            clipboard.Copy();
                            return true;
                        }
                        break;

                    case Key.V:
                        if (!isTextBoxFocused() && clipboard.CanPaste)
                        {
                            clipboard.Paste();
                            return true;
                        }
                        break;

                    case Key.D:
                        if (!isTextBoxFocused() && clipboard.CanCopy)
                        {
                            clipboard.Clone();
                            return true;
                        }
                        break;
                }
            }

            // Transport keys — only when no textbox is focused (textbox consumes its own input
            // before the event bubbles here, but we double-check to be safe).
            if (!isTextBoxFocused())
            {
                if (!e.ControlPressed && !e.AltPressed && !e.SuperPressed)
                {
                    switch (e.Key)
                    {
                        case Key.Space:
                            togglePlayPause();
                            return true;

                        case Key.Z:
                            // Seek to track start
                            editorClock.Seek(0);
                            return true;

                        case Key.X:
                            // Play from start
                            editorClock.Seek(0);
                            editorClock.Start();
                            return true;

                        case Key.C:
                            // Pause/resume
                            if (editorClock.IsRunning) editorClock.Stop(); else editorClock.Start();
                            return true;

                        case Key.V:
                            // Seek to end
                            editorClock.Seek(editorClock.TrackLength);
                            return true;

                        case Key.Left:
                            editorClock.SeekBackward(snapped: true);
                            return true;

                        case Key.Right:
                            editorClock.SeekForward(snapped: true);
                            return true;

                        case Key.Up:
                            // Decrease divisor (fewer subdivisions, coarser grid)
                            beatDivisor.SelectPrevious();
                            return true;

                        case Key.Down:
                            // Increase divisor (more subdivisions, finer grid)
                            beatDivisor.SelectNext();
                            return true;
                    }
                }
            }

            return base.OnKeyDown(e);
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            // Mouse wheel over the compose area: seek one divisor step.
            // Wheel-down (negative ScrollDelta.Y in osu-framework) = forward in time.
            // Wheel-up   (positive ScrollDelta.Y)                   = backward in time.
            // Matches osu's Editor.cs: scrollAccumulation > 0 → seek(e, -1) (backward).
            if (!isTextBoxFocused() && !e.ControlPressed && !e.AltPressed && !e.SuperPressed)
            {
                double scrollY = e.ScrollDelta.Y;
                if (scrollY < 0)
                    editorClock.SeekForward(snapped: true);
                else if (scrollY > 0)
                    editorClock.SeekBackward(snapped: true);

                return true;
            }

            return base.OnScroll(e);
        }

        private void togglePlayPause()
        {
            if (editorClock.IsRunning)
                editorClock.Stop();
            else
                editorClock.Start();
        }

        private bool isTextBoxFocused()
        {
            var focusManager = GetContainingFocusManager();
            var inputManager = focusManager as osu.Framework.Input.InputManager;
            return inputManager?.FocusedDrawable is osu.Framework.Graphics.UserInterface.TextBox;
        }

        // --- Dialog helpers ---

        private void showConfirmThenRun(Action? continuation)
        {
            var dialog = ConfirmDialog.SaveDiscardCancel(
                save: () =>
                {
                    if (ChartFile.FilePath != null)
                    {
                        // Has a path — write directly, then continue.
                        ChartFile.Save();
                        hashAtLastSave = changeHandler.CurrentStateHash;
                        continuation?.Invoke();
                    }
                    else
                    {
                        // No path — open SaveAs dialog; continuation only fires after the file
                        // is actually written (inside the SaveAsDialog completion callback).
                        // Cancelling SaveAs leaves the editor dirty and open.
                        var saveAsDialog = new SaveAsDialog(path =>
                        {
                            ChartFile.Save(path);
                            hashAtLastSave = changeHandler.CurrentStateHash;
                            continuation?.Invoke();
                        }, defaultFilename: string.IsNullOrEmpty(ChartFile.Chart.Metadata.Title) ? "new-chart" : ChartFile.Chart.Metadata.Title);

                        dialogOverlay.Child = saveAsDialog;
                        saveAsDialog.Show();
                    }
                },
                discard: () => continuation?.Invoke()
            );

            dialogOverlay.Child = dialog;
            dialog.Show();
        }
    }
}
