using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

using MainCore;
using MainCore.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using ReactiveMarbles.Extensions.Hosting.ReactiveUI;
using ReactiveMarbles.Extensions.Hosting.Wpf;

using ReactiveUI.Builder;
using ReactiveUI.Primitives;

using Serilog;

using Splat;

using WPFUI.Views;

namespace WPFUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>Raises the <see cref="Application.Startup"/> event.</summary>
        /// <param name="e">A <see cref="StartupEventArgs"/> that contains the event data.</param>
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            UpdateSplash.Show(AppUpdate.CurrentVersion());
            var hostBuilder = AppMixins.GetHostBuilder()
                .ConfigureServices(services => services.AddHostedService<AdminWebHost>())
                .ConfigureSplatForMicrosoftDependencyResolver()
                .ConfigureWpf(wpfBuilder => wpfBuilder
                    .UseCurrentApplication(this)
                    .UseWindow(typeof(MainWindow)));

            using var locator = new ModernDependencyResolver();
            locator.CreateReactiveUIBuilder()
               .WithExceptionHandler(new LoggingExceptionObserver())
               .WithCoreServices()
               .BuildApp();

            var host = hostBuilder
                .Build();

            host.MapSplatLocator(sp =>
            {
                SetupDialogService(sp);
                sp.GetRequiredService<RxQueue>().Setup();
            });

            await host.RunAsync();
        }

        private static void SetupDialogService(IServiceProvider serviceProvider)
        {
            var dialogService = serviceProvider.GetRequiredService<DialogService>();
            dialogService.MessageBox.RegisterHandler(context =>
            {
                ShowMessage(context.Input.Title, context.Input.Message);
                context.SetOutput(RxVoid.Default);
            });

            dialogService.ConfirmBox.RegisterHandler(context =>
            {
                var result = ShowConfirm(context.Input.Title, context.Input.Message);
                context.SetOutput(result);
            });

            dialogService.OkCancelBox.RegisterHandler(context =>
            {
                var result = ShowOkCancel(context.Input.Title, context.Input.Message);
                context.SetOutput(result);
            });

            dialogService.InputBox.RegisterHandler(context =>
            {
                var result = ShowInput(context.Input.Title, context.Input.Message);
                context.SetOutput(result);
            });

            dialogService.FileOpenDialog.RegisterHandler(context =>
            {
                var result = OpenFileDialog();
                context.SetOutput(result);
            });

            dialogService.FileSaveDialog.RegisterHandler(context =>
            {
                var result = SaveFileDialog();
                context.SetOutput(result);
            });
        }

        private static void ShowMessage(string title, string message)
        {
            MessageBox.Show(message, title);
        }

        private static bool ShowConfirm(string title, string message)
        {
            var answer = MessageBox.Show(message, title, MessageBoxButton.YesNo);
            return answer == MessageBoxResult.Yes;
        }

        private static bool ShowOkCancel(string title, string message)
        {
            var answer = MessageBox.Show(message, title, MessageBoxButton.OKCancel);
            return answer == MessageBoxResult.OK;
        }

        private static string ShowInput(string title, string message)
        {
            if (Current.Dispatcher.CheckAccess()) return ShowInputCore(title, message);
            return Current.Dispatcher.Invoke(() => ShowInputCore(title, message));
        }

        private static string ShowInputCore(string title, string message)
        {
            var window = new Window
            {
                Title = title,
                Width = 420,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Owner = Current.MainWindow,
            };

            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
            var textBox = new TextBox { Margin = new Thickness(0, 8, 0, 8) };
            panel.Children.Add(textBox);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            var ok = new Button { Content = "Ok", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancel = new Button { Content = "Cancelar", Width = 80, IsCancel = true };
            var result = "";
            ok.Click += (_, _) =>
            {
                result = textBox.Text;
                window.DialogResult = true;
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            window.Content = panel;
            textBox.Focus();
            return window.ShowDialog() == true ? result : "";
        }

        private static string SaveFileDialog()
        {
            var svd = new Microsoft.Win32.SaveFileDialog
            {
                InitialDirectory = AppContext.BaseDirectory,
                Filter = "TBS files (*.tbs)|*.tbs|All files (*.*)|*.*",
            };
            if (svd.ShowDialog() != true) return "";
            return svd.FileName;
        }

        private static string OpenFileDialog()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                InitialDirectory = AppContext.BaseDirectory,
                Filter = "TBS files (*.tbs)|*.tbs|All files (*.*)|*.*",
            };
            if (ofd.ShowDialog() != true) return "";
            return ofd.FileName;
        }

        private sealed class LoggingExceptionObserver : IObserver<Exception>
        {
            /// <inheritdoc/>
            public void OnNext(Exception value)
            {
                Trace
                    .TraceError($"[ReactiveUI] Unhandled exception: {value}");
                Log
                    .ForContext<LoggingExceptionObserver>()
                    .Error(value, "Unhandled exception");
                MessageBox.Show("There is something wrong. Please check logs/logs-Other.txt.", "Error");

                if (!Debugger.IsAttached) return;
                Debugger.Break();
            }

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnError(Exception error) => OnNext(error);

            /// <inheritdoc/>
            public void OnCompleted()
            {
            }
        }
    }
}