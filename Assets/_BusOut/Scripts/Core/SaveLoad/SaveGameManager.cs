using System;
using System.IO;
using UnityEngine;

namespace BusOut.Core
{
    public static class SaveGameManager
    {
        private static readonly string SECRET_KEY = "MySecretKey1312";

        [Serializable]
        private class SaveUtilityWrapper<T>
        {
            public T value;
        }

        #region SAVE / LOAD (eData)
        public static void Save<T>(eData filename, T data)
        {
            Save(filename.ToString(), data);
        }

        public static T Load<T>(eData filename, T defaultValue)
        {
            return Load(filename.ToString(), defaultValue);
        }
        #endregion

        #region SAVE / LOAD (string key)
        public static void Save<T>(string key, T data)
        {
            try
            {
                string path = GetFilePath(key);
                var wrapper = new SaveUtilityWrapper<T> { value = data };
                string json = JsonUtility.ToJson(wrapper);
                File.WriteAllBytes(path, EncryptDecrypt(json));
            }
            catch (Exception e)
            {
                Debug.LogError($"Error saving data with key {key}: {e.Message}");
            }
        }

        public static T Load<T>(string key, T defaultValue)
        {
            string path = GetFilePath(key);

            if (!File.Exists(path))
            {
                return defaultValue;
            }

            try
            {
                byte[] data = File.ReadAllBytes(path);
                string json = ByteToString(data);
                var wrapper = JsonUtility.FromJson<SaveUtilityWrapper<T>>(json);
                return wrapper != null ? wrapper.value : defaultValue;
            }
            catch (Exception e)
            {
                Debug.LogError($"Save file corrupted or key changed for {key}: {e.Message}");
                return defaultValue;
            }
        }
        #endregion

        #region UTILITIES
        public static bool HasSave(eData filename)
        {
            return HasSave(filename.ToString());
        }

        public static bool HasSave(string key)
        {
            string path = GetFilePath(key);
            return File.Exists(path);
        }

        public static void DeleteSave(eData filename)
        {
            DeleteSave(filename.ToString());
        }

        public static void DeleteSave(string key)
        {
            string path = GetFilePath(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public static void DeleteAllSave()
        {
            if (Directory.Exists(Application.persistentDataPath))
            {
                DirectoryInfo d = new DirectoryInfo(Application.persistentDataPath);
                foreach (var file in d.GetFiles("*.dat"))
                {
                    try
                    {
                        File.Delete(file.FullName);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error deleting file {file.FullName}: {e.Message}");
                    }
                }
            }
        }

        public static void ClearAllData()
        {
            DeleteAllSave();
            PlayerPrefs.DeleteAll();
            Debug.Log("Cleared all save data and PlayerPrefs.");
        }

        public static string GetFilePath(string key)
        {
            return Application.persistentDataPath + "/" + ByteString(EncryptDecrypt(key)) + ".dat";
        }

        public static string GetFilePath(eData filename)
        {
            return GetFilePath(filename.ToString());
        }
        #endregion

        #region ENCRYPTION
        private static byte[] EncryptDecrypt(string input)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(input);
            byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(SECRET_KEY);

            for (int i = 0; i < data.Length; i++)
                data[i] ^= keyBytes[i % keyBytes.Length];

            return data;
        }

        private static byte[] EncryptDecrypt(byte[] input)
        {
            if (input == null) return null;
            byte[] copy = new byte[input.Length];
            Array.Copy(input, copy, input.Length);
            byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(SECRET_KEY);

            for (int i = 0; i < copy.Length; i++)
                copy[i] ^= keyBytes[i % keyBytes.Length];

            return copy;
        }

        public static string ByteToString(byte[] data)
        {
            return System.Text.Encoding.UTF8.GetString(EncryptDecrypt(data));
        }

        public static string ByteString(byte[] data)
        {
            if (data == null) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                sb.Append(data[i].ToString("X2"));
            }
            return sb.ToString();
        }
        #endregion
    }
}
