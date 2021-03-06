using Avalonia.Controls;
using AvalonStudio.Documents;
using AvalonStudio.Extensibility.Dialogs;
using Dock.Model;
using System.Collections.Generic;

namespace AvalonStudio.Shell
{
	public interface IShell
	{
		IDocumentTabViewModel SelectedDocument { get; set; }

        void Select(object view);

        ModalDialogViewModelBase ModalDialog { get; set; }

		void AddDocument(IDocumentTabViewModel document, bool temporary = true);

		void RemoveDocument(IDocumentTabViewModel document);

		IReadOnlyList<IDocumentTabViewModel> Documents { get; }

		IDock Layout { get; }

		IPanel Overlay { get; }
	}
}
