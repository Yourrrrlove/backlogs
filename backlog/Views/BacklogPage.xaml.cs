﻿using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using backlog.Models;
using backlog.Saving;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Animation;
using backlog.Logging;
using backlog.Utils;
using System.Globalization;
using System.Linq;
using Windows.UI.Core;
using Windows.ApplicationModel.DataTransfer;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Diagnostics;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using Windows.System.Profile;
using System.Windows.Input;
using MvvmHelpers.Commands;
using System.ComponentModel;
using Windows.UI.Input.Inking;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace backlog.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class BacklogPage : Page, INotifyPropertyChanged
    {
        private bool _inProgress;
        private bool _showEditControls;
        private bool _hideEditControls = true;
        private bool _isLoading;
        private bool _showProgressSwitch;
        private bool _enableNotificationToggle;
        private bool _showNotificationToggle;
        private bool _showNotificationOptions;
        private DateTimeOffset _calendarDate;
        private TimeSpan _notifTime = TimeSpan.Zero;
        private double _userRating;
        private string _sourceName;
        private Uri _sourceLink;

        public ObservableCollection<Backlog> Backlogs;
        public Backlog Backlog;

        public ICommand LaunchBingSearchResults { get; }
        public ICommand CloseWebViewTrailer { get; }
        public ICommand OpenWebViewTrailer { get; }
        public ICommand ShareBacklog { get; }
        public ICommand StopEditing { get; }
        public ICommand StartEditing { get; }
        public ICommand SaveChanges { get; }
        public ICommand CloseBacklog { get; }
        public ICommand DeleteBacklog { get; }
        public ICommand LaunchRatingDialog { get; }
        public ICommand HideRatingDialog { get; }
        public ICommand CompleteBacklog { get; }
        public ICommand ReadMore { get; }
        public event PropertyChangedEventHandler PropertyChanged;
        public delegate Task LaunchWebView(string video);
        public delegate void NavigateToPreviousPage();
        public delegate void CloseRatingPopup();
        public delegate Task ShowRatingPopup();

        public LaunchWebView LaunchWebViewFunc;
        public NavigateToPreviousPage NavigateToPreviousPageFunc;
        public CloseRatingPopup CloseRatingPopupFunc;
        public ShowRatingPopup ShowRatingPopupFunc;

        public DateTime Today { get; } = DateTime.Today;

        public bool InProgress
        {
            get => _inProgress;
            set
            {
                _inProgress = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InProgress)));
                if (_inProgress)
                {
                    Backlog.Progress = Backlog.Length = 1;
                }
                else
                {
                    Backlog.Progress = Backlog.Length = 0;
                }
            }
        }

        public bool ShowProgressSwitch
        {
            get => _showProgressSwitch;
            set
            {
                _showProgressSwitch = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowProgressSwitch)));
            }
        }

        public bool ShowEditControls
        {
            get => _showEditControls;
            set
            {
                _showEditControls = value;
                HideEditControls = !_showEditControls;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowEditControls)));
            }
        }

        public bool HideEditControls
        {
            get => _hideEditControls;
            set
            {
                _hideEditControls = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HideEditControls)));
            }
        }

        public bool EnableNotificationToggle
        {
            get => _enableNotificationToggle;
            set
            {
                _enableNotificationToggle = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnableNotificationToggle)));
            }
        }

        public bool ShowNotificationToggle
        {
            get => _showNotificationToggle;
            set
            {
                _showNotificationToggle = value;
                if(_showNotificationToggle)
                {
                    ShowNotificationOptions = true;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowNotificationToggle)));
            }
        }

        public bool ShowNotificationOptions
        {
            get => _showNotificationOptions;
            set
            {
                _showNotificationOptions = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowNotificationOptions)));
            }
        }

        public DateTimeOffset CalendarDate
        {
            get => _calendarDate;
            set
            {
                _calendarDate = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CalendarDate)));
                EnableNotificationToggle = _calendarDate != DateTimeOffset.MinValue;
            }
        }

        public TimeSpan NotifTime
        {
            get => _notifTime;
            set
            {
                _notifTime = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NotifTime)));
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
            }
        }

        public double UserRating
        {
            get => _userRating;
            set
            {
                _userRating = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UserRating)));
            }
        }

        public string SourceName
        {
            get => _sourceName;
            set
            {
                _sourceName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SourceName)));
            }
        }

        public Uri SourceLink
        {
            get => _sourceLink;
            set
            {
                _sourceLink = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SourceLink)));
            }
        }


        private int backlogIndex;
        bool signedIn;
        PageStackEntry prevPage;
        StorageFolder tempFolder = ApplicationData.Current.TemporaryFolder;


        public BacklogPage()
        {
            this.InitializeComponent();
            LaunchBingSearchResults = new AsyncCommand(LaunchBingSearchResultsAsync);
            CloseWebViewTrailer = new Command(CloseWebView);
            OpenWebViewTrailer = new AsyncCommand(PlayTrailerAsync);
            ShareBacklog = new AsyncCommand(ShareBacklogAsync);
            StopEditing = new Command(FinishEditing);
            StartEditing = new Command(EnableEditing);
            SaveChanges = new AsyncCommand(SaveChangesAsync);
            CloseBacklog = new AsyncCommand(CloseBacklogAsync);
            DeleteBacklog = new AsyncCommand(DeleteBacklogAsync);
            LaunchRatingDialog = new AsyncCommand(OpenRatingDialogAsync);
            HideRatingDialog = new Command(CloseRatingDialog);
            CompleteBacklog = new AsyncCommand(CompleteBacklogAsync);
            ReadMore = new AsyncCommand(ReadMoreAsync);

            LaunchWebViewFunc = LaunchTrailerWebView;
            NavigateToPreviousPageFunc = NavigateToPrevPageCallback;
            ShowRatingPopupFunc = ShowRatingDialogCallbackAsync;
            CloseRatingPopupFunc = CloseRatingDialogCallback;
            CalendarDate = DateTimeOffset.MinValue;

            Backlogs = SaveData.GetInstance().GetBacklogs();
            signedIn = Settings.IsSignedIn;
            var view = SystemNavigationManager.GetForCurrentView();
            view.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Disabled;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Guid selectedId = (Guid)e.Parameter;
            foreach (Backlog b in Backlogs)
            {
                if (selectedId == b.id)
                {
                    Backlog = b;
                    switch(Backlog.Type)
                    {
                        case "Film":
                            SourceName = "IMdB";
                            SourceLink = new Uri("https://www.imdb.com/");
                            break;
                        case "Album":
                            SourceName = "LastFM";
                            SourceLink = new Uri("https://www.last.fm/");
                            PlayTrailerButton.Visibility = Visibility.Collapsed;
                            break;
                        case "TV":
                            SourceName = "IMdB";
                            SourceLink = new Uri("https://www.imbd.com");
                            break;
                        case "Game":
                            SourceName = "IGDB";
                            SourceLink = new Uri("https://www.igdb.com/discover");
                            break;
                        case "Book":
                            SourceName = "Google Books";
                            SourceLink = new Uri("https://books.google.com/");
                            PlayTrailerButton.Visibility = Visibility.Collapsed;
                            break;
                    }
                    backlogIndex = Backlogs.IndexOf(b);
                }
            }
            ShowProgressSwitch = !Backlog.ShowProgress;
            if(ShowProgressSwitch)
            {
              InProgress = Backlog.Progress > 0;
            }
            base.OnNavigatedTo(e);
            prevPage = Frame.BackStack.Last();
            ConnectedAnimation imageAnimation = ConnectedAnimationService.GetForCurrentView().GetAnimation("cover");
            imageAnimation?.TryStart(img);
        }

        private async Task ReadMoreAsync()
        {
            ContentDialog contentDialog = new ContentDialog
            {
                Title = Backlog.Name,
                Content = Backlog.Description,
                CloseButtonText = "Close"
            };
            await contentDialog.ShowAsync();
        }


        /// <summary>
        /// Deletes the backlog
        /// </summary>
        /// <returns></returns>
        private async Task DeleteBacklogAsync()
        {
            try
            {
                await Logger.Info("Deleting backlog.....");
            }
            catch { }
            ContentDialog deleteDialog = new ContentDialog
            {
                Title = "Delete backlog?",
                Content = "Deletion is permanent. This backlog cannot be recovered, and will be gone forever.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel"
            };
            ContentDialogResult result = await deleteDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await DeleteConfirmation_Click();
            }
            try
            {
                await Logger.Info("Deleted backlog");
            }
            catch { }
        }


        /// <summary>
        /// Delete a backlog after confirmation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async Task DeleteConfirmation_Click()
        {
            IsLoading = true;
            Backlogs.Remove(Backlog);
            SaveData.GetInstance().SaveSettings(Backlogs);
            await SaveData.GetInstance().WriteDataAsync(signedIn);
            NavigateToPreviousPageFunc();
        }

        /// <summary>
        /// Close the backlog
        /// </summary>
        /// <returns></returns>
        private async Task CloseBacklogAsync()
        {
            await SaveBacklog();
            NavigateToPreviousPageFunc();
        }

        private void NavigateToPrevPageCallback()
        {
            Frame.Navigate(prevPage?.SourcePageType, backlogIndex, new SuppressNavigationTransitionInfo());
        }

        private async Task OpenRatingDialogAsync()
        {
            await ShowRatingPopupFunc();
        }

        private async Task ShowRatingDialogCallbackAsync()
        {
            await RatingDialog.ShowAsync();
        }

        /// <summary>
        /// Write backlog to the json file locally and on OneDrive if signed-in
        /// </summary>
        /// <returns></returns>
        private async Task SaveBacklog()
        {
            try
            {
                await Logger.Info("Saving backlog....");
            }catch { }
            Backlogs[backlogIndex] = Backlog;
            SaveData.GetInstance().SaveSettings(Backlogs);
            await SaveData.GetInstance().WriteDataAsync(signedIn);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

        /// <summary>
        /// Marks backlog as complete
        /// </summary>
        /// <returns></returns>
        private async Task CompleteBacklogAsync()
        {
            try
            {
                await Logger.Info("Marking backlog as complete");
            }
            catch { }
            Backlog.IsComplete = true;
            Backlog.UserRating = UserRating;
            Backlog.CompletedDate = DateTimeOffset.Now.Date.ToString("d", CultureInfo.InvariantCulture);
            await SaveBacklog();
            CloseRatingPopupFunc();
            NavigateToPreviousPageFunc();
        }

        private void CloseRatingDialog()
        {
            CloseRatingPopupFunc();
        }

        private void CloseRatingDialogCallback()
        {
            RatingDialog.Hide();
        }

        /// <summary>
        /// Enable editing
        /// </summary>
        private void EnableEditing()
        {
            ShowEditControls = true;
        }

        /// <summary>
        /// Validate and save changes made to the backlog
        /// </summary>
        /// <returns></returns>
        public async Task SaveChangesAsync()
        {
            string date = CalendarDate.DateTime.ToString("D", CultureInfo.InvariantCulture);
            if (ShowNotificationOptions)
            {
                if (NotifTime == TimeSpan.Zero)
                {
                    ContentDialog contentDialog = new ContentDialog
                    {
                        Title = "Invalid date and time",
                        Content = "Please pick a time!",
                        CloseButtonText = "Ok"
                    };
                    await contentDialog.ShowAsync();
                    return;
                }
                DateTimeOffset dateTime = DateTimeOffset.Parse(date, CultureInfo.InvariantCulture).Add(TimePicker.Time);
                int diff = DateTimeOffset.Compare(dateTime, DateTimeOffset.Now);
                if (diff < 0)
                {
                    ContentDialog contentDialog = new ContentDialog
                    {
                        Title = "Invalid time",
                        Content = "The date and time you've chosen are in the past!",
                        CloseButtonText = "Ok"
                    };
                    await contentDialog.ShowAsync();
                    return;
                }
            }
            else
            {
                int diff = DateTime.Compare(DateTime.Today, CalendarDate.DateTime);
                if (diff > 0)
                {
                    ContentDialog contentDialog = new ContentDialog
                    {
                        Title = "Invalid date and time",
                        Content = "The date and time you've chosen are in the past!",
                        CloseButtonText = "Ok"
                    };
                    await contentDialog.ShowAsync();
                    return;
                }
            }
            IsLoading = true;
            Backlog.TargetDate = CalendarDate.ToString("D", CultureInfo.InvariantCulture);
            Backlog.NotifTime = NotifTime;
            ScheduleNotification();
            FinishEditing();
            IsLoading = false;
        }

        /// <summary>
        /// Create notification and send it to Windows
        /// </summary>
        private void ScheduleNotification()
        {
            if (Backlog.NotifTime != TimeSpan.Zero)
            {
                var notifTime = DateTimeOffset.Parse(Backlog.TargetDate, CultureInfo.InvariantCulture).Add(Backlog.NotifTime);
                var builder = new ToastContentBuilder()
                    .AddText($"It's {Backlog.Name} time!")
                    .AddText($"You wanted to check out {Backlog.Name} by {Backlog.Director} today. Get to it!")
                    .AddHeroImage(new Uri(Backlog.ImageURL));
                ScheduledToastNotification toastNotification = new ScheduledToastNotification(builder.GetXml(), notifTime);
                ToastNotificationManager.CreateToastNotifier().AddToSchedule(toastNotification);
            }
        }

        /// <summary>
        /// Stop editing
        /// </summary>
        private void FinishEditing()
        {
            ShowEditControls = false;
        }

        /// <summary>
        /// Open Windows share window to share backlog
        /// </summary>
        /// <returns></returns>
        private async Task ShareBacklogAsync()
        {
            IsLoading = true;
            StorageFile backlogFile = await tempFolder.CreateFileAsync($"{Backlog.Name}.bklg", CreationCollisionOption.ReplaceExisting);
            string json = JsonConvert.SerializeObject(Backlog);
            await FileIO.WriteTextAsync(backlogFile, json);
            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += DataTransferManager_DataRequested;
            DataTransferManager.ShowShareUI();
            IsLoading = false;
        }

        private async void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            DataRequest dataRequest = args.Request;
            dataRequest.Data.Properties.Title = $"Share {Backlog.Name} backlog";
            dataRequest.Data.Properties.Description = "Your contacts with the Backlogs app installed can open this file and add it to their backlog";
            var fileToShare = await tempFolder.GetFileAsync($"{Backlog.Name}.bklg");
            List<IStorageItem> list = new List<IStorageItem>();
            list.Add(fileToShare);
            dataRequest.Data.SetStorageItems(list);
        }

        /// <summary>
        /// Plays the first Youtube result found for "*Name* Offical Trailer"
        /// </summary>
        private async Task PlayTrailerAsync()
        {
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = Keys.YOUTUBE_KEY,
                ApplicationName = "Backlogs"
            });
            var searchListRequest = youtubeService.Search.List("snippet");
            searchListRequest.Q = Backlog.Name + " offical trailer";
            searchListRequest.MaxResults = 1;

            var searchListResponse = await searchListRequest.ExecuteAsync();

            List<string> videos = new List<string>();

            foreach (var searchResult in searchListResponse.Items)
            {
                videos.Add(searchResult.Id.VideoId);
            }
            await LaunchWebViewFunc(videos[0]);
        }

        private async Task LaunchTrailerWebView(string video)
        {
            await trailerDialog.ShowAsync();
            try
            {
                trailerDialog.CornerRadius = new CornerRadius(0); // Without this, for some fucking reason, buttons inside the WebView do not work
            }
            catch
            {

            }
            string width = AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Mobile" ? "600" : "500";
            string height = AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Mobile" ? "100%" : "400";
            webView.NavigateToString($"<iframe width=\"{width}\" height=\"{height}\" src=\"https://www.youtube.com/embed/{video}?autoplay={Settings.AutoplayVideos}\" title=\"YouTube video player\"  allow=\"accelerometer; autoplay; encrypted-media; gyroscope;\"></iframe>");

        }

        /// <summary>
        /// Navigate to a blank page because audio keeps playing after closing the WebView for some reason
        /// </summary>
        private void CloseWebView()
        {
            webView.Navigate(new Uri("about:blank"));
        }

        /// <summary>
        /// Launch default browser to show Bing results
        /// </summary>
        private async Task LaunchBingSearchResultsAsync()
        {
            string searchTerm = Backlog.Name;
            if (Backlog.Type == "Album" || Backlog.Type == "Book")
            {
                searchTerm += $" {Backlog.Director}";
            }
            var searchQuery = searchTerm.Replace(" ", "+");
            var searchUri = new Uri($"https://www.bing.com/search?q={searchQuery}");
            await Windows.System.Launcher.LaunchUriAsync(searchUri);
        }
    }
}
