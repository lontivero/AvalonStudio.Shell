using Avalonia.Input;
using AvalonStudio.Commands;
using AvalonStudio.Docking;
using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Extensibility.Dialogs;
using AvalonStudio.Extensibility.Shell;
using AvalonStudio.MainMenu;
using AvalonStudio.Menus.ViewModels;
using AvalonStudio.MVVM;
using AvalonStudio.Shell.Controls;
using AvalonStudio.Shell.Extensibility.Platforms;
using AvalonStudio.Toolbars;
using AvalonStudio.Toolbars.ViewModels;
using Dock.Model;
using Dock.Model.Controls;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;

namespace AvalonStudio.Shell
{
	[Export(typeof(ShellViewModel))]
	[Export(typeof(IShell))]
	[Shared]
	public class ShellViewModel : ViewModel, IShell
	{
		public static ShellViewModel Instance { get; set; }
		private List<KeyBinding> _keyBindings;

		private IDocumentTabViewModel _selectedDocument;

		private IEnumerable<Lazy<IExtension>> _extensions;
		private IEnumerable<Lazy<ToolViewModel>> _toolControls;
		private CommandService _commandService;
		private List<IDocumentTabViewModel> _documents;

		private Lazy<StatusBarViewModel> _statusBar;

		private ModalDialogViewModelBase modalDialog;

		private DocumentDock _documentDock;
		private ToolDock _leftPane;
		private ToolDock _rightPane;
		private ToolDock _bottomPane;

		private IDockFactory _factory;
		private IDock _layout;

		[ImportingConstructor]
		public ShellViewModel(
			CommandService commandService,
			Lazy<StatusBarViewModel> statusBar,
			MainMenuService mainMenuService,
			ToolbarService toolbarService,
			[ImportMany] IEnumerable<Lazy<IExtension>> extensions,
			[ImportMany] IEnumerable<Lazy<ToolViewModel>> toolControls)
		{
			_extensions = extensions;
			_toolControls = toolControls;

			_commandService = commandService;

			MainMenu = mainMenuService.GetMainMenu();

			var toolbars = toolbarService.GetToolbars();
			StandardToolbar = toolbars.Single(t => t.Key == "Standard").Value;

			_statusBar = statusBar;

			_keyBindings = new List<KeyBinding>();

			ModalDialog = new ModalDialogViewModelBase("Dialog");

			_documents = new List<IDocumentTabViewModel>();
		}

		public void Initialise(IDockFactory layoutFactory = null)
		{
			foreach (var extension in _extensions)
			{
				if (extension.Value is IActivatableExtension activatable)
				{
					activatable.BeforeActivation();
				}
			}

			if (layoutFactory == null)
			{
				Factory = new DefaultLayoutFactory();
			}
			else
			{
				Factory = layoutFactory;
			}

			LoadLayout();

			Layout.WhenAnyValue(l => l.FocusedView).Subscribe(focused =>
			{
				if (focused is IDocumentTabViewModel doc)
				{
					SelectedDocument = doc;
				}
				else
				{
					SelectedDocument = null;
				}
			});

			if (Layout.Factory.ViewLocator.ContainsKey("LeftDock"))
			{
				_leftPane = Layout.Factory.ViewLocator["LeftDock"]() as ToolDock;
			}

			if (Layout.Factory.ViewLocator.ContainsKey("DocumentDock"))
			{
				_documentDock = Layout.Factory.ViewLocator["DocumentDock"]() as DocumentDock;
			}

			if (Layout.Factory.ViewLocator.ContainsKey("RightDock"))
			{
				_rightPane = Layout.Factory.ViewLocator["RightDock"]() as ToolDock;
			}

			if (Layout.Factory.ViewLocator.ContainsKey("BottomDock"))
			{
				_bottomPane = Layout.Factory.ViewLocator["BottomDock"]() as ToolDock;
			}

			foreach (var extension in _extensions)
			{
				if (extension.Value is IActivatableExtension activatable)
				{
					activatable.Activation();
				}
			}

			foreach (var command in _commandService.GetKeyGestures())
			{
				foreach (var keyGesture in command.Value)
				{
					_keyBindings.Add(new KeyBinding { Command = command.Key.Command, Gesture = KeyGesture.Parse(keyGesture) });
				}
			}

			foreach (var tool in _toolControls)
			{
				switch (tool.Value.DefaultLocation)
				{
					case Location.Bottom:
						DockView(_bottomPane, tool.Value);
						break;

					//case Location.BottomRight:
					//    BottomRightTabs.Tools.Add(tool);
					//    break;

					//case Location.RightBottom:
					//    RightBottomTabs.Tools.Add(tool);
					//    break;

					//case Location.RightMiddle:
					//    RightMiddleTabs.Tools.Add(tool);
					//    break;

					//case Location.RightTop:
					//    RightTopTabs.Tools.Add(tool);
					//    break;

					//case Location.MiddleTop:
					//    MiddleTopTabs.Tools.Add(tool);
					//    break;

					case Location.Left:
						DockView(_leftPane, tool.Value);
						break;

					case Location.Right:
						DockView(_rightPane, tool.Value);
						break;
				}
			}

			IoC.Get<IStatusBar>().ClearText();
		}

		public string Title => Platform.AppName;

		private void DockView(IDock dock, IView view, bool add = true)
		{
			if (add)
			{
				dock.Views.Add(view);
				Factory.Update(view, view, dock);
			}
			else
			{
				Factory.Update(view, view, view.Parent);
			}

			Factory.SetCurrentView(view);
		}

		public IReadOnlyList<IDocumentTabViewModel> Documents => _documents.AsReadOnly();

		public IDockFactory Factory
		{
			get => _factory;
			set => this.RaiseAndSetIfChanged(ref _factory, value);
		}

		public IDock Layout
		{
			get => _layout;
			set => this.RaiseAndSetIfChanged(ref _layout, value);
		}

		public void LoadLayout()
		{
			//string path = System.IO.Path.Combine(Platform.SettingsDirectory, "Layout.json");

			//if (DockSerializer.Exists(path))
			//{
			//    //Layout = DockSerializer.Load<RootDock>(path);
			//}

			if (Layout == null)
			{
				Layout = Factory.CreateLayout();
				Factory.InitLayout(Layout, this);
			}
		}

		public void CloseLayout()
		{
            Layout.Close();
		}

		public void SaveLayout()
		{
			//string path = System.IO.Path.Combine(Platform.SettingsDirectory, "Layout.json");
			//DockSerializer.Save(path, Layout);
		}

		public MenuViewModel MainMenu { get; }

		public StatusBarViewModel StatusBar => _statusBar.Value;

		private ToolbarViewModel StandardToolbar { get; }

		public IEnumerable<KeyBinding> KeyBindings => _keyBindings;

		public void AddDocument(IDocumentTabViewModel document, bool temporary = false)
		{
			DockView(_documentDock, document, !Documents.Contains(document));

			_documents.Add(document);
		}

		public void RemoveDocument(IDocumentTabViewModel document)
		{
			if (document == null)
			{
				return;
			}

			// TODO implement save on close.

			if (document.Parent is IDock dock)
			{
				dock.Views.Remove(document);
				Factory.Update(document, document, dock);
			}

			_documents.Remove(document);
		}

		public ModalDialogViewModelBase ModalDialog
		{
			get { return modalDialog; }
			set { this.RaiseAndSetIfChanged(ref modalDialog, value); }
		}

		public IDocumentTabViewModel SelectedDocument
		{
			get => _selectedDocument;
			set
			{
				(_selectedDocument as IDocumentTabViewModel)?.OnDeselected();

				if (value != null)
				{
					Factory.SetCurrentView(value);
				}

				_selectedDocument = value;

				(_selectedDocument as IDocumentTabViewModel)?.OnSelected();

				this.RaisePropertyChanged(nameof(SelectedDocument));
			}
		}

        public void Select (object view)
        {
            if(view is IDocumentTabViewModel doc)
            {
                SelectedDocument = doc;
            }
            else if (view is ToolViewModel tool)
            {
                Layout.Factory.SetCurrentView(tool);
            }
        }

		public void AddOrSelectDocument<T>(T document) where T : IDocumentTabViewModel
		{
			IDocumentTabViewModel doc = Documents.FirstOrDefault(x => x.Equals(document));

			if (doc != null)
			{
				SelectedDocument = doc;
			}
			else
			{
				AddDocument(document);
			}
		}

		public void AddOrSelectDocument<T>(Func<T> factory) where T : IDocumentTabViewModel
		{
			IDocumentTabViewModel doc = Documents.FirstOrDefault(x => x is T);

			if (doc != default)
			{
				SelectedDocument = doc;
			}
			else
			{
				AddDocument(factory());
			}
		}

		public T GetOrCreate<T>() where T : IDocumentTabViewModel, new()
		{
			T document = default;

			IDocumentTabViewModel doc = Documents.FirstOrDefault(x => x is T);

			if (doc != default)
			{
				document = (T)doc;
				SelectedDocument = doc;
			}
			else
			{
				document = new T();
				AddDocument(document);
			}
			return document;
		}

		public Avalonia.Controls.IPanel Overlay { get; internal set; }
	}
}
