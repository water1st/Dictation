using System.Collections;
using System.Collections.Generic;

namespace Dictation.Core
{
    public class WordCollection : IEnumerable<string>
    {
        private List<string> words = new List<string>();
        // 添加单词
        public void AddWord(string word)
        {
            if (!string.IsNullOrWhiteSpace(word) && !words.Contains(word))
            {
                words.Add(word);
            }
        }

        public int Count => words.Count;

        // 删除单词
        public void RemoveWord(string word)
        {
            words.Remove(word);
        }

        // 清空单词
        public void ClearWords()
        {
            words.Clear();
        }

        // 获取所有单词
        public IEnumerable<string> GetWords()
        {
            return words.ToArray();
        }

        public IEnumerator<string> GetEnumerator()
        {
            foreach (var word in words)
                yield return word;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public static WordCollection operator +(WordCollection collection, string word)
        {
            collection.words.Add(word);
            return collection;
        }

        public static WordCollection operator -(WordCollection collection, string word)
        {
            collection.words.Remove(word);
            return collection;
        }
    }
}
