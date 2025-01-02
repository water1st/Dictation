using System.Collections;
using System.Collections.Generic;

namespace Dictation.Core
{
    public class WordDrawingCollection : IEnumerable<WordDrawing>
    {
        private List<WordDrawing> list = new List<WordDrawing>();
        public void Add(WordDrawing word)
        {
            if (word != null)
            {
                list.Add(word);
            }
        }

        public WordDrawing this[int index]
        {
            get { return list[index]; }
        }

        public int Count => list.Count;

        public void Clear()
        {
            list.Clear();
        }

        public IEnumerator<WordDrawing> GetEnumerator()
        {
            foreach (WordDrawing word in list)
                yield return word;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
