﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.Globalization;
using Android.Preferences;
using Domain.Services;

namespace Droid.Services
{
    [Preserve(AllMembers = true)]
    public class SettingsService : ISettingsService
    {
        private readonly object locker = new object();

        /// <summary>
        /// Gets the current value or the default that you specify.
        /// </summary>
        /// <typeparam name="T">Vaue of t (bool, int, float, long, string)</typeparam>
        /// <param name="key">Key for settings</param>
        /// <param name="defaultValue">default value if not set</param>
        /// <param name="fileName">Name of file for settings to be stored and retrieved (iOS = SuiteName, Android = Name, Windows Store/RT8.1/UWP = Container name, WinPhone 8 SL = Doesn't Apply)</param>
        /// <returns>Value or default</returns>
        public T GetValueOrDefault<T>(string key, T defaultValue = default(T), string fileName = null)
        {
            lock (locker)
            {
                using (var sharedPreferences = GetSharedPreference(fileName))
                {
                    return GetValueOrDefaultCore(sharedPreferences, key, defaultValue);
                }
            }
        }

        private T GetValueOrDefaultCore<T>(ISharedPreferences sharedPreferences, string key, T defaultValue)
        {
            Type typeOf = typeof(T);
            if (typeOf.IsGenericType && typeOf.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                typeOf = Nullable.GetUnderlyingType(typeOf);
            }

            object value = null;
            var typeCode = Type.GetTypeCode(typeOf);
            bool resave = false;
            switch (typeCode)
            {
                case TypeCode.Decimal:
                    //Android doesn't have decimal in shared prefs so get string and convert
                    var savedDecimal = string.Empty;
                    try
                    {
                        savedDecimal = sharedPreferences.GetString(key, string.Empty);
                    }
                    catch (Java.Lang.ClassCastException)
                    {
                        Console.WriteLine("Settings 1.5 change, have to remove key.");

                        try
                        {
                            Console.WriteLine("Attempting to get old value.");
                            savedDecimal =
                                sharedPreferences.GetLong(key,
                                    (long)Convert.ToDecimal(defaultValue, System.Globalization.CultureInfo.InvariantCulture))
                                    .ToString();
                            Console.WriteLine("Old value has been parsed and will be updated and saved.");
                        }
                        catch (Java.Lang.ClassCastException)
                        {
                            Console.WriteLine("Could not parse old value, will be lost.");
                        }
                        Remove(key);
                        resave = true;
                    }
                    if (string.IsNullOrWhiteSpace(savedDecimal))
                        value = Convert.ToDecimal(defaultValue, System.Globalization.CultureInfo.InvariantCulture);
                    else
                        value = Convert.ToDecimal(savedDecimal, System.Globalization.CultureInfo.InvariantCulture);

                    if (resave)
                        AddOrUpdateValue(key, value);

                    break;
                case TypeCode.Boolean:
                    value = sharedPreferences.GetBoolean(key, Convert.ToBoolean(defaultValue));
                    break;
                case TypeCode.Int64:
                    value =
                        (Int64)
                            sharedPreferences.GetLong(key,
                                (long)Convert.ToInt64(defaultValue, System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case TypeCode.String:
                    value = sharedPreferences.GetString(key, Convert.ToString(defaultValue));
                    break;
                case TypeCode.Double:
                    //Android doesn't have double, so must get as string and parse.
                    var savedDouble = string.Empty;
                    try
                    {
                        savedDouble = sharedPreferences.GetString(key, string.Empty);
                    }
                    catch (Java.Lang.ClassCastException)
                    {
                        Console.WriteLine("Settings 1.5  change, have to remove key.");

                        try
                        {
                            Console.WriteLine("Attempting to get old value.");
                            savedDouble =
                                sharedPreferences.GetLong(key,
                                    (long)Convert.ToDouble(defaultValue, System.Globalization.CultureInfo.InvariantCulture))
                                    .ToString();
                            Console.WriteLine("Old value has been parsed and will be updated and saved.");
                        }
                        catch (Java.Lang.ClassCastException)
                        {
                            Console.WriteLine("Could not parse old value, will be lost.");
                        }
                        Remove(key);
                        resave = true;
                    }

                    if (string.IsNullOrWhiteSpace(savedDouble))
                        value = defaultValue;
                    else
                    {
                        double outDouble;

                        if (!double.TryParse(savedDouble, NumberStyles.Number, CultureInfo.InvariantCulture, out outDouble))
                        {
                            var maxString = Convert.ToString(double.MaxValue, System.Globalization.CultureInfo.InvariantCulture);
                            outDouble = savedDouble.Equals(maxString) ? double.MaxValue : double.MinValue;
                        }

                        value = outDouble;
                    }

                    if (resave)
                        AddOrUpdateValue(key, value);

                    break;
                case TypeCode.Int32:
                    value = sharedPreferences.GetInt(key,
                        Convert.ToInt32(defaultValue, System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case TypeCode.Single:
                    value = sharedPreferences.GetFloat(key,
                        Convert.ToSingle(defaultValue, System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case TypeCode.DateTime:
                    if (sharedPreferences.Contains(key))
                    {
                        var ticks = sharedPreferences.GetLong(key, 0);
                        if (ticks >= 0)
                        {
                            //Old value, stored before update to UTC values
                            value = new DateTime(ticks);
                        }
                        else
                        {
                            //New value, UTC
                            value = new DateTime(-ticks, DateTimeKind.Utc);
                        }
                    }
                    else
                    {
                        return defaultValue;
                    }
                    break;
                default:

                    if (defaultValue is Guid)
                    {
                        var outGuid = Guid.Empty;
                        Guid.TryParse(sharedPreferences.GetString(key, Guid.Empty.ToString()), out outGuid);
                        value = outGuid;
                    }
                    else
                    {
                        throw new ArgumentException($"Value of type {typeCode} is not supported.");
                    }

                    break;
            }

            return null != value ? (T)value : defaultValue;
        }

        /// <summary>
        /// Adds or updates a value
        /// </summary>
        /// <param name="key">key to update</param>
        /// <param name="value">value to set</param>
        /// <param name="fileName">Name of file for settings to be stored and retrieved (iOS = SuiteName, Android = Name, Windows Store/RT8.1/UWP = Container name, WinPhone 8 SL = Doesn't Apply)</param>
        /// <returns>True if added or update and you need to save</returns>
        public bool AddOrUpdateValue<T>(string key, T value, string fileName = null)
        {
            Type typeOf = typeof(T);
            if (typeOf.IsGenericType && typeOf.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                typeOf = Nullable.GetUnderlyingType(typeOf);
            }
            var typeCode = Type.GetTypeCode(typeOf);
            return AddOrUpdateValue(key, value, typeCode, fileName);
        }

        private bool AddOrUpdateValue(string key, object value, TypeCode typeCode, string fileName)
        {
            lock (locker)
            {
                using (var sharedPreferences = GetSharedPreference(fileName))
                {
                    using (var sharedPreferencesEditor = sharedPreferences.Edit())
                    {
                        switch (typeCode)
                        {
                            case TypeCode.Decimal:
                                sharedPreferencesEditor.PutString(key,
                                    Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                                break;
                            case TypeCode.Boolean:
                                sharedPreferencesEditor.PutBoolean(key, Convert.ToBoolean(value));
                                break;
                            case TypeCode.Int64:
                                sharedPreferencesEditor.PutLong(key,
                                    (long)Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
                                break;
                            case TypeCode.String:
                                sharedPreferencesEditor.PutString(key, Convert.ToString(value));
                                break;
                            case TypeCode.Double:
                                var valueString = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                                sharedPreferencesEditor.PutString(key, valueString);
                                break;
                            case TypeCode.Int32:
                                sharedPreferencesEditor.PutInt(key,
                                    Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
                                break;
                            case TypeCode.Single:
                                sharedPreferencesEditor.PutFloat(key,
                                    Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture));
                                break;
                            case TypeCode.DateTime:
                                sharedPreferencesEditor.PutLong(key, -(Convert.ToDateTime(value)).ToUniversalTime().Ticks);
                                break;
                            default:
                                if (value is Guid)
                                {
                                    sharedPreferencesEditor.PutString(key, ((Guid)value).ToString());
                                }
                                else
                                {
                                    throw new ArgumentException($"Value of type {typeCode} is not supported.");
                                }
                                break;
                        }

                        sharedPreferencesEditor.Commit();
                    }
                }
            }

            return true;
        }


        /// <summary>
        /// Removes a desired key from the settings
        /// </summary>
        /// <param name="key">Key for setting</param>
        /// <param name="fileName">Name of file for settings to be stored and retrieved (iOS = SuiteName, Android = Name, Windows Store/RT8.1/UWP = Container name, WinPhone 8 SL = Doesn't Apply)</param>
        public void Remove(string key, string fileName = null)
        {
            lock (locker)
            {
                using (var sharedPreferences = GetSharedPreference(fileName))
                {
                    using (var sharedPreferencesEditor = sharedPreferences.Edit())
                    {
                        sharedPreferencesEditor.Remove(key);
                        sharedPreferencesEditor.Commit();
                    }
                }
            }
        }

        /// <summary>
        /// Clear all keys from settings
        /// </summary>
        /// <param name="fileName">Name of file for settings to be stored and retrieved (iOS = SuiteName, Android = Name, Windows Store/RT8.1/UWP = Container name, WinPhone 8 SL = Doesn't Apply)</param>
        public void Clear(string fileName = null)
        {
            lock (locker)
            {
                using (var sharedPreferences = GetSharedPreference(fileName))
                {
                    using (var sharedPreferencesEditor = sharedPreferences.Edit())
                    {
                        sharedPreferencesEditor.Clear();
                        sharedPreferencesEditor.Commit();
                    }
                }
            }
        }

        /// <summary>
        /// Checks to see if the key has been added.
        /// </summary>
        /// <param name="key">Key to check</param>
        /// <param name="fileName">Name of file for settings to be stored and retrieved (iOS = SuiteName, Android = Name, Windows Store/RT8.1/UWP = Container name, WinPhone 8 SL = Doesn't Apply)</param>
        /// <returns>True if contains key, else false</returns>
        public bool Contains(string key, string fileName = null)
        {
            lock (locker)
            {
                using (var sharedPreferences = GetSharedPreference(fileName))
                {
                    return sharedPreferences.Contains(key);
                }
            }
        }

        ISharedPreferences GetSharedPreference(string fileName) =>
            string.IsNullOrWhiteSpace(fileName) ?
            PreferenceManager.GetDefaultSharedPreferences(Application.Context) :
            Application.Context.GetSharedPreferences(fileName, FileCreationMode.Private);
    }
}