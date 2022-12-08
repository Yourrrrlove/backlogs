﻿using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Backlogs.Models;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Animation;
using Backlogs.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Backlogs.Services;
using Backlogs.Utils;
using System.Diagnostics;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Backlogs.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        int backlogIndex = -1;

        public MainViewModel ViewModel { get; set; }

        public MainPage()
        {
            this.InitializeComponent();
            ViewModel = new MainViewModel(App.Services.GetRequiredService<INavigation>(), App.Services.GetRequiredService<IDialogHandler>(),
                App.Services.GetRequiredService<IShareDialogService>(), App.Services.GetRequiredService<IUserSettings>(),
                App.Services.GetRequiredService<IFileHandler>(), App.Services.GetRequiredService<ILiveTileService>(),
                App.Services.GetRequiredService<IFilePicker>(), App.Services.GetRequiredService<IEmailService>(),
                App.Services.GetService<IMsal>(), App.Services.GetRequiredService<ISystemLauncher>());
            
            var view = SystemNavigationManager.GetForCurrentView();
            view.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Disabled;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter != null && e.Parameter.ToString() != "")
            {
                if (e.Parameter.ToString() == "sync")
                {
                    ViewModel.Sync = true;
                }
                else
                {
                    // for backward connected animation
                    backlogIndex = int.Parse(e.Parameter.ToString());
                }
            }
            await ViewModel.SetupProfile();
        }        
    }
}
