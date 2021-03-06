﻿using System;
using PhotoSharingApp.Frontend.Portable.Models;
using System.Linq;
using System.Threading.Tasks;
using PhotoSharingApp.Frontend.Portable.Services;
using PhotoSharingApp.Frontend.Portable.Exceptions;
using GalaSoft.MvvmLight.Command;
using PhotoSharingApp.Frontend.Portable.Abstractions;
using GalaSoft.MvvmLight.Views;
using IDialogService = PhotoSharingApp.Frontend.Portable.Abstractions.IDialogService;

namespace PhotoSharingApp.Frontend.Portable
{
    public class PhotoDetailsViewModel : AsyncViewModelBase
    {
        private INavigationService navigationService;
        private IDialogService dialogService;
        private IConnectivityService connectivityService;
        private IPhotoService photoService;

        private Photo photo;
        public Photo Photo
        {
            get { return photo; }
            set { photo = value; RaisePropertyChanged(); }
        }

        private bool isCurrentUsersPhoto;
        public bool IsCurrentUsersPhoto
        {
            get { return isCurrentUsersPhoto; }
            set { isCurrentUsersPhoto = value; RaisePropertyChanged(); }
        }

        private RelayCommand deletePhotoCommand;
        public RelayCommand DeletePhotoCommand
        {
            get
            {
                return deletePhotoCommand ?? (deletePhotoCommand = new RelayCommand(async () =>
                {
                    await DeletePhotoAsync();
                }));
            }
        }

        private RelayCommand setAsProfilePictureCommand;
        public RelayCommand SetAsProfilePictureCommand
        {
            get
            {
                return setAsProfilePictureCommand ?? (setAsProfilePictureCommand = new RelayCommand(async () =>
                {
                    await SetAsProfilePictureAsync();
                }));
            }
        }

        private RelayCommand<Annotation> addAnnotationCommand;
        public RelayCommand<Annotation> AddAnnotationCommand
        {
            get
            {
                return addAnnotationCommand ?? (addAnnotationCommand = new RelayCommand<Annotation>(async (Annotation annotation) =>
                {
                    await AddCommentAsync(annotation.Text, annotation.GoldCount);
                }));
            }
        }

        public PhotoDetailsViewModel(INavigationService navigationService, IDialogService dialogService, IConnectivityService connectivityService, IPhotoService photoService)
        {
            this.navigationService = navigationService;
            this.dialogService = dialogService;
            this.connectivityService = connectivityService;
            this.photoService = photoService;
        }

        public async Task RefreshAsync()
        {
            // Check connectivity
            if (!connectivityService.IsConnected())
            {
                return;
            }

            IsLoading = true;

            // Update photo with detailed information
            Photo = await photoService.GetPhotoDetails(Photo.Id);

            // Check if photo is a current user's one
            try
            {
                var currentUser = await photoService.GetCurrentUser();
                if (currentUser.UserId == Photo.User.UserId)
                    IsCurrentUsersPhoto = true;
                else
                    IsCurrentUsersPhoto = false;
            }
            catch (UnauthorizedException)
            {
                IsCurrentUsersPhoto = false;
            }

            IsLoading = false;
        }

        public async Task DeletePhotoAsync()
        {
            // Check connectivity
            if (!connectivityService.IsConnected())
            {
                await ShowNoConnectionDialog(dialogService);
                return;
            }

            // Confirm deletion
            if (!await dialogService.DisplayDialogAsync("Delete photo", "Do you really want to delete this photo?", "Delete", "Cancel"))
                return;

            try
            {
                // Delete photo
                await photoService.DeletePhoto(Photo);
                navigationService.GoBack();
            }
            catch (Exception ex)
            {
                await dialogService.DisplayDialogAsync("Failed to delete", "Could not delete photo", "Ok");
            }
        }

        public async Task SetAsProfilePictureAsync()
        {
            // Check connectivity
            if (!connectivityService.IsConnected())
            {
                await ShowNoConnectionDialog(dialogService);
                return;
            }

            // Confirm profile picture
            if (!await dialogService.DisplayDialogAsync("Set as profile picture", "Do you really want to set this photo as your profile picture?", "Yes", "Cancel"))
                return;

            try
            {
                // Set profile picture
                await photoService.GetCurrentUser();
                await photoService.UpdateUserProfilePhoto(Photo);
                await dialogService.DisplayDialogAsync("Profile Picture updated", "", "Ok");
            }
            catch (Exception ex)
            {
                await dialogService.DisplayDialogAsync("Failed to delete", "Could not delete photo", "Ok");
            }
        }

        private async Task AddCommentAsync(string text, int gold)
        {
            // Check connectivity
            if (!connectivityService.IsConnected())
            {
                await ShowNoConnectionDialog(dialogService);
                return;
            }

            try
            {
                IsLoading = true;

                // Check if user is logged in
                await photoService.GetCurrentUser();

                var annotation = await photoService.PostAnnotation(Photo, text, gold);
                Photo.Annotations.Add(annotation);
                Photo.GoldCount += gold;

                IsLoading = false;
            }
            catch (UnauthorizedException)
            {
                await dialogService.DisplayDialogAsync("Not logged in!", "You need to be logged in to upload a photo. Please log in first.", "Ok");
            }
            catch (InsufficientBalanceException)
            {
                await dialogService.DisplayDialogAsync("Balance too low!", "You don't have enough gold.", "Ok");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
