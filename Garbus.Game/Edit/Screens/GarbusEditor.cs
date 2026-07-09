// Editor shell: tabs, menu bar, hotkeys, dirty tracking, DI caching.

using System;
using System.Collections.Generic;
using Garbus.Game.Charts;
using Garbus.Game.Configuration;
using Garbus.Game.Edit.Screens.Dialogs;
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

        // --- Private state ---

        private EditorClock editorClock = null!;
        private GarbusChartChangeHandler changeHandler = null!;
        private BindableBeatDivisor beatDivisor = null!;

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

            dependencies.Cache(editorClock);
            dependencies.Cache(EditorChart);
            dependencies.Cache(changeHandler);
            dependencies.CacheAs<IEditorChangeHandler>(changeHandler);
            dependencies.Cache(beatDivisor);
            dependencies.CacheAs(this);
            dependencies.CacheAs(ChartFile);
            // Cache ControlPointInfo directly so timeline components (TimelineTickDisplay,
            // TimelineTimingChangeDisplay) can resolve it without going through EditorChart.
            dependencies.CacheAs(EditorChart.ControlPointInfo);

            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audioManager)
        {
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

            editorClock.ChangeSource(track);
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
                    new MenuItem("Timing"),
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

            return new[] { undoItem, redoItem };
        }

        private Drawable createBottomBar()
        {
            // Placeholder; content arrives in Task 17/18.
            return new Container
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 60,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new osuTK.Graphics.Color4(20, 20, 28, 255),
                },
            };
        }

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
                }
            }

            return base.OnKeyDown(e);
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
