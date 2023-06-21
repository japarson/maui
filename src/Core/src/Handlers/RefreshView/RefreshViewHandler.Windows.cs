﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using WBrush = Microsoft.UI.Xaml.Media.Brush;

namespace Microsoft.Maui.Handlers
{
	public partial class RefreshViewHandler : ViewHandler<IRefreshView, RefreshContainer>
	{
		bool _isLoaded;
		Deferral? _refreshCompletionDeferral;

		protected override RefreshContainer CreatePlatformView()
		{
			var refreshContainer = new RefreshContainer
			{
				ManipulationMode = UI.Xaml.Input.ManipulationModes.All,
				PullDirection = RefreshPullDirection.TopToBottom
			};

			return refreshContainer;
		}

		protected override void ConnectHandler(RefreshContainer nativeView)
		{
			nativeView.Loaded += OnLoaded;
			nativeView.RefreshRequested += OnRefresh;
			nativeView.ManipulationDelta += OnManipulationDelta;

			base.ConnectHandler(nativeView);
		}

		protected override void DisconnectHandler(RefreshContainer nativeView)
		{
			nativeView.Loaded -= OnLoaded;
			nativeView.RefreshRequested -= OnRefresh;
			nativeView.ManipulationDelta += OnManipulationDelta;

			CompleteRefresh();

			base.DisconnectHandler(nativeView);
		}

		public static void MapVisualizationState(IRefreshViewHandler handler, IRefreshView refreshView)
		{
			return;
		}

		public static void MapIsRefreshing(IRefreshViewHandler handler, IRefreshView refreshView)
			=> (handler as RefreshViewHandler)?.UpdateIsRefreshing();

		public static void MapContent(IRefreshViewHandler handler, IRefreshView refreshView)
			=> UpdateContent(handler);

		public static void MapRefreshColor(IRefreshViewHandler handler, IRefreshView refreshView)
			=> UpdateRefreshColor(handler);

		public static void MapRefreshViewBackground(IRefreshViewHandler handler, IView view)
			=> UpdateBackground(handler);

		void UpdateIsRefreshing()
		{
			if (!_isLoaded)
				return;

			if (!VirtualView?.IsRefreshing ?? false)
				CompleteRefresh();
			else if (_refreshCompletionDeferral == null)
				PlatformView?.RequestRefresh();
		}

		static void UpdateContent(IRefreshViewHandler handler)
		{
			handler.PlatformView.Content =
				handler.VirtualView.Content?.ToPlatform(handler.MauiContext!);
		}

		static void UpdateRefreshColor(IRefreshViewHandler handler)
		{
			if (handler.VirtualView == null || handler.PlatformView?.Visualizer == null)
				return;

			handler.PlatformView.Visualizer.Foreground = handler.VirtualView.RefreshColor != null
				? handler.VirtualView.RefreshColor.ToPlatform()
				: (WBrush)UI.Xaml.Application.Current.Resources["DefaultTextForegroundThemeBrush"];
		}

		static void UpdateBackground(IRefreshViewHandler handler)
		{
			if (handler.PlatformView?.Visualizer == null)
				return;

			if (handler.VirtualView.Background != null)
				handler.PlatformView.Visualizer.Background = handler.VirtualView.Background.ToPlatform();
		}

		// Telling the refresh to start before the control has been sized
		// causes no refresh circle to show up
		void OnLoaded(object sender, object args)
		{
			var refreshControl = sender as RefreshContainer;

			if (refreshControl == null || MauiContext == null)
				return;

			refreshControl.Loaded -= OnLoaded;
			MauiContext.Services
				.GetRequiredService<IDispatcher>()
				.Dispatch(() =>
				{
					_isLoaded = true;
					UpdateIsRefreshing();
				});
		}

		void OnRefresh(object sender, RefreshRequestedEventArgs args)
		{
			CompleteRefresh();
			_refreshCompletionDeferral = args.GetDeferral();

			if (VirtualView != null)
				VirtualView.IsRefreshing = true;
		}

		void OnManipulationDelta(object sender, UI.Xaml.Input.ManipulationDeltaRoutedEventArgs e)
		{
			if (e.PointerDeviceType is UI.Input.PointerDeviceType.Touch)
				return; // Already managed by the RefreshContainer control itself

			const double minimumCumulativeY = 20;
			double cumulativeY = e.Cumulative.Translation.Y;

			if (cumulativeY > minimumCumulativeY && VirtualView is not null && !VirtualView.IsRefreshing)
				VirtualView.IsRefreshing = true;
		}

		void CompleteRefresh()
		{
			if (_refreshCompletionDeferral != null)
			{
				_refreshCompletionDeferral.Complete();
				_refreshCompletionDeferral.Dispose();
				_refreshCompletionDeferral = null;
			}
		}
	}
}
