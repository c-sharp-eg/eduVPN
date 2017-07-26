﻿/*
    eduVPN - End-user friendly VPN

    Copyright: 2017, The Commons Conservancy eduVPN Programme
    SPDX-License-Identifier: GPL-3.0+
*/

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace eduVPN.Client.Converters
{
    /// <summary>
    /// Returns <c>Visibility.Visible</c> if user is disabled; or <c>Visibility.Collapsed</c> otherwise.
    /// </summary>
    public class UserDisabledVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Models.UserInfo user_info && !user_info.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
