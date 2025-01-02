using System.Drawing;

namespace Dictation.Core
{
    public class WordDrawing
    {
        public string Word { get; set; }
        public Bitmap Drawing { get; set; }
        public bool IsCorrect { get; set; }
        public bool IsJudged { get; set; }

        public WordDrawing(string word, Bitmap drawing)
        {
            Word = word;
            Drawing = drawing;
            IsJudged = false;
        }


        public void Judge(bool isCorrect)
        {
            IsCorrect = isCorrect;
            IsJudged = true;
        }
    }
}
