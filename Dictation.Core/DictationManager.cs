using System;
using System.Collections.Generic;
using System.Drawing;

namespace Dictation.Core
{
    public class DictationManager : IDisposable
    {
        private readonly WordDrawingCollection wordDrawings;
        private readonly ITTSPlayer player;
        private readonly List<string> worlds;
        private string currentWorld;
        private int count;
        private int current = 0;

        public DictationManager(WordCollection wordManager, WordDrawingCollection wordDrawings, ITTSPlayer player)
        {
            worlds = new List<string>(wordManager);
            count = wordManager.Count;
            this.wordDrawings = wordDrawings;
            this.player = player;
        }

        public void PlayCurrentWord()
        {
            if (!string.IsNullOrEmpty(currentWorld))
            {
                player.Play(currentWorld);
            }

        }

        public int WordCount => count;

        public int Current => current;

        public IEnumerable<string> Stop()
        {
            var result = worlds.ToArray();
            worlds.Clear();
            return result;
        }

        public bool IsEnd => worlds.Count == 0;

        public string PlayNextWord()
        {
            if (worlds.Count > 0)
            {
                current++;
                Random random = new Random();
                var index = random.Next(worlds.Count);

                var word = worlds[index];
                player.Play(word);

                worlds.RemoveAt(index);

                currentWorld = word;

                return word;
            }

            return null;
        }


        public void SaveBitmap(string word, Bitmap bitmap)
        {
            if (!string.IsNullOrEmpty(word) && bitmap != null)
                wordDrawings.Add(new WordDrawing(word, bitmap));
        }

        public void Dispose()
        {
            player.Dispose();
        }
    }
}
