// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <windows.h>

// No CRT startup or stack-protected functions reference a security cookie.
const IMAGE_LOAD_CONFIG_DIRECTORY _load_config_used = {
    .Size = sizeof(IMAGE_LOAD_CONFIG_DIRECTORY),
    .GuardFlags = IMAGE_GUARD_SECURITY_COOKIE_UNUSED
};

int main(void)
{
    return 0;
}
