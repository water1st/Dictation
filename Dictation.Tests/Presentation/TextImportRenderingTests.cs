using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证文本词表在单窗口控件内展示候选、中文义项和批量操作。</summary>
    public sealed class TextImportRenderingTests
    {
        [Fact]
        public void Page_RendersMultilineEditorSelectAllAndInboxActions()
        {
            RunOnSta(() =>
            {
                var page = new TextImportPage();
                Render(page);
                Assert.Contains(Children<TextBox>(page), x => x.AcceptsReturn);
                Assert.Contains(Children<CheckBox>(page), x => Equals(x.Content, "全选已确认义项"));
                Assert.Contains(Children<Button>(page), x => Equals(x.Content, "匹配词表"));
                Assert.Contains(Children<Button>(page), x => Equals(x.Content, "加入收件箱"));
                Assert.Contains(Children<Button>(page), x => Equals(x.Content, "打开收件箱"));
                Assert.DoesNotContain(Children<Button>(page), x => Equals(x.Content, "切换日语") || Equals(x.Content, "切换英语"));
            });
        }

        [Fact]
        public void Page_BindsSenseChoicesWithoutSelectingFirstAmbiguousCandidate()
        {
            RunOnSta(() =>
            {
                var first = Result("jmdict:1:1", "吃");
                var second = Result("jmdict:1:2", "用餐");
                var row = new TextImportLineViewModel(new TextImportLineDto(1, "食べる", new[] { first, second }));
                var page = new TextImportPage { DataContext = new { CanEdit = true, Lines = new[] { row } } };
                Render(page);
                var selector = Assert.Single(Children<ComboBox>(page));
                Assert.Equal(2, selector.Items.Count);
                Assert.Equal(-1, selector.SelectedIndex);

                selector.SelectedItem = second;
                Render(page);

                Assert.Same(second, row.SelectedCandidate);
                Assert.True(row.IsSelected);
                Assert.Contains(Children<TextBlock>(page), x => x.Text == "用餐");
            });
        }

        private static LexiconLookupResultDto Result(string id, string meaning) => new(
            LearningLanguage.Japanese, id, "jmdict:1", "食べる", "たべる", "动词", "二类・他动词",
            false, new[] { meaning }, Array.Empty<LexiconLookupFormDto>());

        private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T item) yield return item;
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                foreach (var child in Children<T>(VisualTreeHelper.GetChild(root, index))) yield return child;
            }
        }

        private static void Render(UserControl page)
        {
            page.Width = 1024;
            page.Height = 720;
            page.Measure(new Size(1024, 720));
            page.Arrange(new Rect(0, 0, 1024, 720));
            page.UpdateLayout();
        }

        private static void RunOnSta(Action action)
        {
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception exception) { error = exception; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}
