using UnityEngine;

public static class JsonHelper
{
    public static T[][] FromJson<T>(string json)
    {
        json = "{\"array\":" + json + "}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
        return wrapper.array;
    }

    [System.Serializable]
    private class Wrapper<T>
    {
        public T[][] array;
    }
}