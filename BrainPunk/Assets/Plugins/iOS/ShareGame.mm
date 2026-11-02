#import <UIKit/UIKit.h>
extern "C" void _ShareText(const char* textC)
{
    NSString *text = textC ? [NSString stringWithUTF8String:textC] : @"";
    NSArray *items = @[text];
    UIActivityViewController *vc =
        [[UIActivityViewController alloc] initWithActivityItems:items applicationActivities:nil];
    UIViewController *root = UIApplication.sharedApplication.keyWindow.rootViewController;
    while (root.presentedViewController) root = root.presentedViewController;
    [root presentViewController:vc animated:YES completion:nil];
}