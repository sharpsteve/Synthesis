﻿using DynamicData;
using DynamicData.Binding;
using Mutagen.Bethesda;
using Noggog;
using Noggog.WPF;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Synthesis.Bethesda.Execution.Patchers;
using Synthesis.Bethesda.Execution.Runner;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Synthesis.Bethesda.GUI
{
    public class RunningPatchersVM : ViewModel
    {
        public ConfigurationVM Config { get; }

        public ProfileVM RunningProfile { get; }

        private CancellationTokenSource _cancel = new CancellationTokenSource();

        [Reactive]
        public Exception? ResultError { get; private set; }

        [Reactive]
        public bool Running { get; private set; } = true;

        public SourceCache<RunningPatcherVM, int> Patchers { get; } = new SourceCache<RunningPatcherVM, int>(p => p.Config.ID);
        public IObservableCollection<RunningPatcherVM> PatchersDisplay { get; }

        [Reactive]
        public RunningPatcherVM? SelectedPatcher { get; set; }

        public ICommand BackCommand { get; }

        private readonly RxReporter<int> _reporter = new RxReporter<int>();

        public RunningPatchersVM(ConfigurationVM parent, ProfileVM profile)
        {
            Config = parent;
            RunningProfile = profile;
            Patchers.AddOrUpdate(RunningProfile.Patchers.Items
                .Where(x => x.IsOn)
                .Select(p => p.ToRunner(this)));
            PatchersDisplay = Patchers.Connect()
                .ToObservableCollection(this);
            if (parent.SelectedPatcher != null
                && Patchers.TryGetValue(parent.SelectedPatcher.ID, out var run))
            {
                SelectedPatcher = run;
            }

            BackCommand = ReactiveCommand.Create(() =>
            {
                parent.SelectedPatcher = SelectedPatcher?.Config;
                parent.MainVM.ActivePanel = parent;
            },
            canExecute: this.WhenAnyValue(x => x.Running)
                .Select(running => !running));

            _reporter.Overall
                .ObserveOnGui()
                .Subscribe(ex =>
                {
                    throw new NotImplementedException();
                })
                .DisposeWith(this);
            _reporter.PrepProblem
                .Merge(_reporter.RunProblem)
                .ObserveOnGui()
                .Subscribe(i =>
                {
                    if (Patchers.TryGetValue(i.Key, out var vm))
                    {
                        vm.State = GetResponse<RunState>.Fail(RunState.Error, i.Error);
                        SelectedPatcher = vm;
                    }
                })
                .DisposeWith(this);
            _reporter.Starting
                .ObserveOnGui()
                .Subscribe(i =>
                {
                    if (Patchers.TryGetValue(i.Key, out var vm))
                    {
                        vm.State = GetResponse<RunState>.Succeed(RunState.Started);
                    }
                })
                .DisposeWith(this);
            _reporter.RunSuccessful
                .ObserveOnGui()
                .Subscribe(i =>
                {
                    if (Patchers.TryGetValue(i.Key, out var vm))
                    {
                        vm.State = GetResponse<RunState>.Succeed(RunState.Finished);
                    }
                })
                .DisposeWith(this);
        }

        public async Task Run()
        {
            await Observable.Return(Unit.Default)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .DoTask(async (_) =>
                {
                    try
                    {
                        var output = Path.Combine(RunningProfile.WorkingDirectory, Synthesis.Bethesda.Constants.SynthesisModKey.FileName);
                        await Runner.Run<int>(
                            workingDirectory: RunningProfile.WorkingDirectory,
                            outputPath: output,
                            dataFolder: RunningProfile.DataFolder,
                            release: RunningProfile.Release,
                            loadOrder: RunningProfile.LoadOrder.Items,
                            cancellation: _cancel.Token,
                            reporter: _reporter,
                            patchers: Patchers.Items.Select(vm => (vm.Config.ID, vm.Run)));
                        var dataFolderPath = Path.Combine(RunningProfile.DataFolder, Synthesis.Bethesda.Constants.SynthesisModKey.FileName);
                        File.Copy(output, dataFolderPath, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        ResultError = ex;
                    }
                })
                .ObserveOnGui()
                .Do(_ =>
                {
                    Running = false;
                });
        }
    }
}
