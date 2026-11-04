using MainCore.UI.Models.Output;

using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace MainCore.Services
{
    [RegisterSingleton<DialogService>]
    public sealed class DialogService
    {
        public Interaction<MessageBoxData, bool> ConfirmBox { get; }
        public Interaction<MessageBoxData, bool> OkCancelBox { get; }
        public Interaction<MessageBoxData, string> InputBox { get; }
        public Interaction<MessageBoxData, RxVoid> MessageBox { get; }
        public Interaction<RxVoid, string> FileOpenDialog { get; }
        public Interaction<RxVoid, string> FileSaveDialog { get; }

        public DialogService()
        {
            ConfirmBox = new Interaction<MessageBoxData, bool>();
            OkCancelBox = new Interaction<MessageBoxData, bool>();
            InputBox = new Interaction<MessageBoxData, string>();
            MessageBox = new Interaction<MessageBoxData, RxVoid>();
            FileOpenDialog = new Interaction<RxVoid, string>();
            FileSaveDialog = new Interaction<RxVoid, string>();
        }

        public async Task SendMessage(string title, string message)
        {
            var messageBoxData = new MessageBoxData(title, message);
            await MessageBox.Handle(messageBoxData);
        }

        public async Task<bool> SendConfirm(string title, string message)
        {
            var messageBoxData = new MessageBoxData(title, message);
            return await ConfirmBox.Handle(messageBoxData);
        }

        public async Task<bool> SendOkCancel(string title, string message)
        {
            var messageBoxData = new MessageBoxData(title, message);
            return await OkCancelBox.Handle(messageBoxData);
        }

        public async Task<string> RequestInput(string title, string message)
        {
            var messageBoxData = new MessageBoxData(title, message);
            return await InputBox.Handle(messageBoxData);
        }

        public async Task<string> OpenFileDialog()
        {
            return await FileOpenDialog.Handle(RxVoid.Default);
        }

        public async Task<string> SaveFileDialog()
        {
            return await FileSaveDialog.Handle(RxVoid.Default);
        }
    }
}