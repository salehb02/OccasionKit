// Alternate app icons for OccasionKit (IOSAppIconBackend). Unity calls these on the main thread.

#import <UIKit/UIKit.h>

// IL2CPP frees returned strings with free(), so hand back a malloc'd copy.
static char* OccasionKitCopyString(NSString* value)
{
    if (value == nil)
        return NULL;

    const char* utf8 = [value UTF8String];
    char* copy = (char*)malloc(strlen(utf8) + 1);
    strcpy(copy, utf8);
    return copy;
}

extern "C"
{
    int OccasionKit_SupportsAlternateIcons()
    {
        return [UIApplication sharedApplication].supportsAlternateIcons ? 1 : 0;
    }

    // Comma-separated alternate icon names from the built Info.plist (Xcode writes them from the asset catalog).
    char* OccasionKit_GetAlternateIconNames()
    {
        NSMutableOrderedSet* names = [NSMutableOrderedSet orderedSet];

        for (NSString* key in @[ @"CFBundleIcons", @"CFBundleIcons~ipad" ])
        {
            id icons = [[NSBundle mainBundle] objectForInfoDictionaryKey:key];
            id alternates = [icons isKindOfClass:[NSDictionary class]] ? icons[@"CFBundleAlternateIcons"] : nil;

            if ([alternates isKindOfClass:[NSDictionary class]])
                [names addObjectsFromArray:[alternates allKeys]];
        }

        return OccasionKitCopyString([names.array componentsJoinedByString:@","]);
    }

    // NULL while the primary icon shows.
    char* OccasionKit_GetAlternateIconName()
    {
        return OccasionKitCopyString([UIApplication sharedApplication].alternateIconName);
    }

    // NULL or empty: back to the primary icon. iOS shows its own alert, and refuses while the app isn't active.
    void OccasionKit_SetAlternateIconName(const char* name)
    {
        UIApplication* application = [UIApplication sharedApplication];

        if (!application.supportsAlternateIcons)
            return;

        NSString* iconName = name != NULL && name[0] != '\0' ? [NSString stringWithUTF8String:name] : nil;

        if ((iconName == nil && application.alternateIconName == nil) || [iconName isEqualToString:application.alternateIconName])
            return;

        [application setAlternateIconName:iconName completionHandler:^(NSError* error) {
            if (error != nil)
                NSLog(@"[OccasionKit] Switching the app icon to %@ failed: %@", iconName ?: @"the primary icon", error.localizedDescription);
        }];
    }
}
