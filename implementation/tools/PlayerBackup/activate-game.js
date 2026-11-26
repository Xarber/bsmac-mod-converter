// SPDX-License-Identifier: AGPL-3.0-only
ObjC.import('AppKit');

function run(args) {
    const bundle = $.NSURL.fileURLWithPath(args[0]).URLByResolvingSymlinksInPath;
    for (let attempt = 0; attempt < 24; attempt++) {
        const apps = $.NSWorkspace.sharedWorkspace.runningApplications;
        for (let index = 0; index < apps.count; index++) {
            const app = apps.objectAtIndex(index);
            if (!app.finishedLaunching || app.terminated || !app.bundleURL) continue;
            if (!app.bundleURL.URLByResolvingSymlinksInPath.isEqual(bundle)) continue;
            if (app.active || app.activateWithOptions(2)) return; // NSApplicationActivateIgnoringOtherApps
        }
        delay(0.5);
    }
    throw new Error('The relaunched Beat Saber instance could not be brought forward');
}
