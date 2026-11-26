#if IOS || MACCATALYST
using System;
using CoreFoundation;
using Foundation;
using UIKit;

static class AppleApp
{
	public static void Main(string[] args) => UIApplication.Main(args, null, typeof(TestAppDelegate));
}

[Register("Box3DTestAppDelegate")]
public class TestAppDelegate : UIApplicationDelegate
{
	public override UISceneConfiguration GetConfiguration(UIApplication application, UISceneSession session, UISceneConnectionOptions options)
		=> new UISceneConfiguration("Default Configuration", session.Role);
}

[Register("Box3DTestSceneDelegate")]
public class TestSceneDelegate : UIResponder, IUIWindowSceneDelegate
{
	[Export("window")]
	public UIWindow? Window { get; set; }

	[Export("scene:willConnectToSession:options:")]
	public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions options)
	{
		if (scene is not UIWindowScene windowScene)
			return;

		Window = new UIWindow(windowScene) { RootViewController = new UIViewController() };
		Window.MakeKeyAndVisible();
		DispatchQueue.MainQueue.DispatchAsync(() => Environment.Exit(Program.RunValidation()));
	}
}
#endif
