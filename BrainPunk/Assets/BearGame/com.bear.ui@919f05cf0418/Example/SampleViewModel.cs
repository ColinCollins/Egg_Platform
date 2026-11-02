using Bear.UI;

namespace Bear.UI.Example
{
    /// <summary>
    /// 示例视图模型
    /// </summary>
    public class SampleViewModel : ViewModel
    {
        private string _title;
        private int _score;

        /// <summary>
        /// 标题
        /// </summary>
        public string Title
        {
            get => GetProperty<string>(nameof(Title));
            set => SetProperty(nameof(Title), value);
        }

        /// <summary>
        /// 分数
        /// </summary>
        public int Score
        {
            get => GetProperty<int>(nameof(Score));
            set => SetProperty(nameof(Score), value);
        }

        public SampleViewModel()
        {
            Title = "Sample UI";
            Score = 0;
        }
    }
}

