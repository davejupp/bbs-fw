using BBSFW.Model;
using BBSFW.ViewModel.Base;
using Microsoft.Win32;
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace BBSFW.ViewModel
{

	public class MainViewModel : ObservableObject
	{
		private const string APP_TITLE = "BBS-FW Tool";
		private string _configFileName;

		public string ConfigFileName
		{
			get { return _configFileName; }
			set
			{
				if (_configFileName != value)
				{
					_configFileName = value;
					ApplicationTitle = ConfigFilenameExists ? APP_TITLE + " - " + System.IO.Path.GetFileName(value) : APP_TITLE; 
					OnPropertyChanged(nameof(ConfigFileName));
					OnPropertyChanged(nameof(ConfigFilenameExists));
				}
			}
		}

		public bool ConfigFilenameExists
		{
			get
			{
				return !String.IsNullOrEmpty(ConfigFileName);
			}
		}

		private string _applicationTitle = "BBS - FW Tool";
		public string ApplicationTitle {
			get { return _applicationTitle; }
			set {
				if (_applicationTitle != value)
				{
					_applicationTitle = value;
					OnPropertyChanged(nameof(ApplicationTitle));
				}
			}
		}

		public ConfigurationViewModel ConfigVm { get; private set; }

		public ConnectionViewModel ConnectionVm { get; private set; }

		public SystemViewModel SystemVm { get; private set; }

		public AssistLevelsViewModel AssistLevelsVm { get; private set; }

		public CalibrationViewModel CalibrationVm { get; private set; }

		public EventLogViewModel EventLogVm { get; private set; }



		public ICommand OpenConfigCommand
		{
			get { return new DelegateCommand(OnOpenConfig); }
		}

		/**
		 * This doesnt actually close the config, it just clears the saved file name so you can't accidentally overwrite
		 */
		public ICommand CloseConfigCommand
		{
			get { return new DelegateCommand(OnCloseConfig); }
		}

		public ICommand SaveAsConfigCommand
		{
			get { return new DelegateCommand(OnSaveAsConfig); }
		}

		public ICommand SaveConfigCommand
		{
			get { return new DelegateCommand(OnSaveConfig); }
		}

		public ICommand SaveLogCommand
		{
			get { return new DelegateCommand(OnSaveLog); }
		}

		public ICommand ReadFlashCommand
		{
			get { return new DelegateCommand(OnReadFlash); }
		}

		public ICommand WriteFlashCommand
		{
			get { return new DelegateCommand(OnWriteFlash); }
		}

		public ICommand ResetFlashCommand
		{
			get { return new DelegateCommand(OnResetFlash); }
		}

		public ICommand ExitCommand
		{
			get { return new DelegateCommand(OnExit); }
		}

		public ICommand ShowAboutCommand
		{
			get { return new DelegateCommand(OnShowAbout); }
		}



		public MainViewModel()
		{
			ConfigVm = new ConfigurationViewModel();

			ConnectionVm = new ConnectionViewModel();
			SystemVm = new SystemViewModel(ConfigVm);
			AssistLevelsVm = new AssistLevelsViewModel(ConfigVm);
			CalibrationVm = new CalibrationViewModel(ConnectionVm);
			EventLogVm = new EventLogViewModel();


			ConnectionVm.EventLogReceived += EventLogVm.AddEvent;
		}


		private void OnSaveLog()
		{
			var dialog = new SaveFileDialog();

			dialog.Filter = "Log File|*.log";
			dialog.Title = "Save Log";
			dialog.FileName = "bbsfw.log";

			var result = dialog.ShowDialog();
			if (result.HasValue && result.Value)
			{
				try
				{
					EventLogVm.ExportLog(dialog.FileName);
				}
				catch(Exception e)
				{
					MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
		}

		private void OnCloseConfig()
		{
			ConfigFileName = null;
		}

		private void OnOpenConfig()
		{
			var dialog = new OpenFileDialog();
			dialog.Filter = "XML File|*.xml";
			dialog.Title = "Open Configuration";

			var result = dialog.ShowDialog();
			if (result.HasValue && result.Value)
			{
				try
				{
					ConfigVm.ReadConfiguration(dialog.FileName);
					ConfigFileName = dialog.FileName;
				}
				catch (Exception e)
				{
					MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
		}

		private void OnSaveConfig()
		{
			if (!ValidateConfig())
			{
				return;
			}

			// This should be impossible anyhow as the menu item should be disabled.
			if (!ConfigFilenameExists)
			{
				OnSaveAsConfig();
				return;
			}

			try
			{
				ConfigVm.WriteConfiguration(ConfigFileName);
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}		

		private void OnSaveAsConfig()
		{
			if (!ValidateConfig())
			{
				return;
			}

			var dialog = new SaveFileDialog();

			dialog.Filter = "XML File|*.xml";
			dialog.Title = "Save Configuration";
			if (String.IsNullOrEmpty(ConfigFileName))
			{
				dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
				dialog.FileName = "bbsfw.xml";
			}
			else
			{
				dialog.InitialDirectory = System.IO.Path.GetDirectoryName(ConfigFileName);
				dialog.FileName =  System.IO.Path.GetFileName(ConfigFileName);
			}

			var result = dialog.ShowDialog();
			if (result.HasValue && result.Value)
			{
				try
				{
					ConfigVm.WriteConfiguration(dialog.FileName);
					// Updating the config file name will also update the application title.
					ConfigFileName = dialog.FileName;
				}
				catch (Exception e)
				{
					MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
		}

		private async void OnReadFlash()
		{
			if (!ConnectionVm.IsConnected)
			{
				return;
			}

			if (!VerifyConfigVersionForRead())
			{
				return;
			}

			var res = await ConnectionVm.GetConnection().ReadConfiguration(TimeSpan.FromSeconds(5));
			if (!res.Timeout && res.Result != null)
			{
				ConfigVm.UpdateFrom(res.Result);
			}
			else
			{
				MessageBox.Show("Failed to read configuration from flash, timeout occured.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private async void OnWriteFlash()
		{
			if (!ConnectionVm.IsConnected)
			{
				return;
			}

			if (!ValidateConfig())
			{
				return;
			}

			if (!VerifyConfigVersionForWrite())
			{
				return;
			}

			var res = await ConnectionVm.GetConnection().WriteConfiguration(ConfigVm.GetConfig(), TimeSpan.FromSeconds(5));
			if (!res.Timeout)
			{
				if (res.Result)
				{
					MessageBox.Show("Configuration Written!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
				}
				else
				{
					MessageBox.Show("Failed to write configuration to flash, try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
			else
			{
				MessageBox.Show("Failed to write configuration to flash, timeout occured.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private async void OnResetFlash()
		{
			if (!ConnectionVm.IsConnected)
			{
				return;
			}

			var res = await ConnectionVm.GetConnection().ResetConfiguration(TimeSpan.FromSeconds(5));
			if (!res.Timeout)
			{
				if (res.Result)
				{
					OnReadFlash();
				}
				else
				{
					MessageBox.Show("Failed to reset configuration, try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
			else
			{
				MessageBox.Show("Failed to reset configuration, timeout occured.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}


		private void OnShowAbout()
		{
			var version = Assembly.GetExecutingAssembly().GetName().Version;
			MessageBox.Show($"Version: {version.Major}.{version.Minor}.{version.Build}\nAuthor: Daniel Nilsson", "BBS-FW Tool", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private void OnExit()
		{
			Application.Current.Shutdown();
		}

		private bool VerifyConfigVersionForRead()
		{
			if (ConnectionVm.ConfigVersion < Configuration.MinVersion || ConnectionVm.ConfigVersion > Configuration.MaxVersion)
			{
				MessageBox.Show("Unsupported firmware config version. Please use BBS-FW Config Tool for firmware version " + ConnectionVm.FirmwareVersion + " to read configuration from flash.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}

			return true;
		}

		private bool VerifyConfigVersionForWrite()
		{
			if (ConnectionVm.ConfigVersion != Configuration.CurrentVersion)
			{
				MessageBox.Show("Unsupported firmware config version. Please use BBS-FW Config Tool for firmware version " + ConnectionVm.FirmwareVersion + " in order to write configuration to flash, or upgrade firmware to latest version.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}

			return true;
		}

		private bool ValidateConfig()
		{
			try
			{
				ConfigVm.GetConfig().Validate();
				return true;
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}

			return false;
		}

	}
}
