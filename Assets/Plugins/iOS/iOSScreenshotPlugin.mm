#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <Photos/Photos.h>

extern "C" {
    void _SaveImageToGallery(const char* path) {
        NSString *filePath = [NSString stringWithUTF8String:path];
        UIImage *image = [UIImage imageWithContentsOfFile:filePath];
        
        if (image != nil) {
            [[PHPhotoLibrary sharedPhotoLibrary] performChanges:^{
                [PHAssetCreationRequest creationRequestForAssetFromImage:image];
            } completionHandler:^(BOOL success, NSError *error) {
                if (success) {
                    NSLog(@"Screenshot saved to Photos successfully!");
                } else {
                    NSLog(@"Failed to save screenshot: %@", error.localizedDescription);
                }
            }];
        }
    }
}
