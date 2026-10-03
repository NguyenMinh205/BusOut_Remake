using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ScreenShot.Helper
{
    public class GameViewSizeChanger : EditorWindow
    {
        public const string Key_LastLabel = "Key_LastLabel";
    }

    public static class Save
    {
        public static void Set<T>(string key, T value)
        {
            EditorUserSettings.SetConfigValue(key, value.ToString());
        }
    }
    public class ResizeToolsHelper
    {
        #region public enum
        public enum GameViewSizeType
        {
            FixedResolution,
            AspectRatio
        }

        #endregion public enum

        #region private Fiald

        static BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance;

        #endregion private Fiald

        #region private Class

        private static GameViewSize _gameViewSize;
        public class GameViewSize
        {
            public GameViewSizeType type;
            public int width;
            public int height;
            public string baseText;
        }

        #endregion private Class

        #region public Method
        /// <summary>
        /// Add Custom Size game view
        /// </summary>
        /// <param name="groupType">Type</param>
        /// <param name="gameViewSize">Size</param>
        public static void AddCustomSize(GameViewSizeGroupType groupType, GameViewSize gameViewSize)
        {
            _gameViewSize = gameViewSize;
            object sizeType = Enum.Parse(Types.gameViewSizeType, gameViewSize.type.ToString());

            ConstructorInfo ctor = Types.gameViewSize.GetConstructor(new Type[]
                {
                    Types.gameViewSizeType,
                    typeof(int),
                    typeof(int),
                    typeof(string)
                });

            object instance_gameViewSize = ctor.Invoke(new object[]
                {
                    sizeType,
                    gameViewSize.width,
                    gameViewSize.height,
                    gameViewSize.baseText
                });

            object instance_gameViewSizeGroup = GetGroup(groupType, instance);

            if (!Contains(instance_gameViewSizeGroup))
            {
                AddCustomSize(instance_gameViewSizeGroup, instance_gameViewSize);
            }
        }
        /// <summary>
        /// Contains
        /// </summary>
        public static bool Contains(GameViewSizeGroupType groupType, GameViewSize gameViewSize)
        {
            _gameViewSize = gameViewSize;
            return Contains(GetGroup(groupType, instance));
        }
        /// <summary>
        /// Change to custom size game view
        /// </summary>
        /// <param name="groupType">Type</param>
        /// <param name="gameViewSize">Size</param>
        public static void ChangeGameViewSize(GameViewSizeGroupType groupType, GameViewSize gameViewSize)
        {
            _gameViewSize = gameViewSize;
            GameViewSizeGroupType currentType = GetCurrentGameViewSizeGroupType();
            if (groupType != currentType)
            {
                Debug.LogError(string.Format("GameViewSizeGroupType is {0}. but Current GameViewSizeGroupType is {1}.", groupType, currentType));
                return;
            }
            object group = GetGroup(groupType, instance);
            int totalCount = GetTotalCount(group);
            int gameViewSizeLength = GetCustomCount(group);
            int index = -1;
            for (int i = totalCount - gameViewSizeLength; i < totalCount; i++)
            {
                object other_gameViewSize = GetGameViewSize(group, i);
                if (GameViewSize_Equals(_gameViewSize, other_gameViewSize))
                {
                    index = i;
                    break;
                }
            }
            if (index != -1)
            {
                PropertyInfo selectedSizeIndex = Types.gameView.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.NonPublic);
                EditorWindow gameView = EditorWindow.GetWindow(Types.gameView);
                selectedSizeIndex.SetValue(gameView, index, null);
            }
        }
        /// <summary>
        /// Get Current Game View Size Group Type
        /// </summary>
        /// <returns>Group Type</returns>
        public static GameViewSizeGroupType GetCurrentGameViewSizeGroupType()
        {
            EditorWindow gameView = EditorWindow.GetWindow(Types.gameView);
            PropertyInfo currentSizeGroupType = Types.gameView.GetProperty("currentSizeGroupType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            return (GameViewSizeGroupType)currentSizeGroupType.GetValue(gameView, null);
        }

        #endregion public Method

        #region private Method
        /// <summary>
        /// Contains
        /// </summary>
        static bool Contains(object instance_gameViewSizeGroup)
        {
            int gameViewSizeLength = GetCustomCount(instance_gameViewSizeGroup);
            int totalCount = GetTotalCount(instance_gameViewSizeGroup);
            for (int i = totalCount - gameViewSizeLength; i < totalCount; i++)
            {
                if (GameViewSize_Equals(_gameViewSize, GetGameViewSize(instance_gameViewSizeGroup, i)))
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Game View Size Equals
        /// </summary>
        /// <returns>true or false</returns>
        private static bool GameViewSize_Equals(GameViewSize a, object b)
        {
            int b_width = (int)GetGameSizeProperty(b, "width");
            int b_height = (int)GetGameSizeProperty(b, "height");
            string b_baseText = (string)GetGameSizeProperty(b, "baseText");
            GameViewSizeType b_sizeType = (GameViewSizeType)Enum.Parse(typeof(GameViewSizeType), GetGameSizeProperty(b, "sizeType").ToString());

            return a.type == b_sizeType && a.width == b_width && a.height == b_height && a.baseText == b_baseText;
        }
        /// <summary>
        /// Get Game Size Property
        /// </summary>
        static object GetGameSizeProperty(object instance, string name)
        {
            return instance.GetType().GetProperty(name).GetValue(instance, new object[0]);
        }

        static object m_instance;

        static object instance
        {
            get
            {
                if (m_instance == null)
                {
                    PropertyInfo propertyInfo_gameViewSizes = Types.gameViewSizes.GetProperty("instance");
                    m_instance = propertyInfo_gameViewSizes.GetValue(null, new object[0]);
                }
                return m_instance;
            }
        }
        /// <summary>
        /// Get Group
        /// </summary>
        /// <returns>Group</returns>
        static object GetGroup(GameViewSizeGroupType groupType, object instance_gameViewSizes)
        {
            Type[] returnTypes = new Type[] { groupType.GetType() };
            object[] parameters = new object[] { groupType };
            return instance_gameViewSizes.GetType().GetMethod("GetGroup",
                bindingFlags,
                null,
                returnTypes,
                null).Invoke(instance_gameViewSizes, parameters);
        }
        /// <summary>
        /// Get Game View Size
        /// </summary>
        /// <returns>Game View Size</returns>
        static object GetGameViewSize(object instance_gameViewSizeGroup, int i)
        {
            Type[] returnTypes = new Type[] { typeof(int) };
            object[] parameters = new object[] { i };
            return instance_gameViewSizeGroup.GetType().GetMethod("GetGameViewSize",
                bindingFlags,
                null,
                returnTypes,
                null).Invoke(instance_gameViewSizeGroup, parameters);
        }
        /// <summary>
        /// Method "GetCustomCount"
        /// </summary>
        /// <returns></returns>
        static int GetCustomCount(object instance_gameViewSizeGroup)
        {
            return (int)instance_gameViewSizeGroup.GetType().GetMethod("GetCustomCount",
                bindingFlags,
                null,
                new Type[0],
                null).Invoke(instance_gameViewSizeGroup, new object[0]);
        }
        /// <summary>
        /// Method "GetTotalCount"
        /// </summary>
        /// <returns></returns>
        static int GetTotalCount(object instance_gameViewSizeGroup)
        {
            return (int)instance_gameViewSizeGroup.GetType().GetMethod("GetTotalCount",
                bindingFlags,
                null,
                new Type[0],
                null).Invoke(instance_gameViewSizeGroup, new object[0]);
        }
        /// <summary>
        /// Method "AddCustomSize"
        /// </summary>
        /// <returns></returns>
        static void AddCustomSize(object instance_gameViewSizeGroup, object instance_gameViewSize)
        {
            Type[] returnTypes = new Type[] { Types.gameViewSize };
            object[] parameters = new object[] { instance_gameViewSize };
            instance_gameViewSizeGroup.GetType().GetMethod("AddCustomSize",
                bindingFlags,
                null,
                returnTypes,
                null).Invoke(instance_gameViewSizeGroup, parameters);
        }
        /// <summary>
        /// Method "RemoveCustomSize"
        /// </summary>
        /// <returns></returns>
        static void RemoveCustomSize(object instance_gameViewSizeGroup, int index)
        {
            Type[] returnTypes = new Type[] { typeof(int) };
            object[] parameters = new object[] { index };
            instance_gameViewSizeGroup.GetType().GetMethod("RemoveCustomSize",
                bindingFlags,
                null,
                returnTypes,
                null).Invoke(instance_gameViewSizeGroup, parameters);
        }

        #endregion private Method

        #region types

        private static class Types
        {
            private static string assemblyName = "UnityEditor.dll";
            private static Assembly assembly = Assembly.Load(assemblyName);

            public static Type gameView = assembly.GetType("UnityEditor.GameView");

            public static Type gameViewSizeType = assembly.GetType("UnityEditor.GameViewSizeType");
            public static Type gameViewSize = assembly.GetType("UnityEditor.GameViewSize");
            public static Type gameViewSizes = assembly.GetType("UnityEditor.ScriptableSingleton`1").MakeGenericType(assembly.GetType("UnityEditor.GameViewSizes"));
        }

        #endregion types

    }
}
