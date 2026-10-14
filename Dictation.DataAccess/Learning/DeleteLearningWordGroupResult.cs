namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示学习库整组删除的持久化结果。
    /// </summary>
    public enum DeleteLearningWordGroupResult
    {
        Deleted,
        NotFound,
        WrongLanguage,
        WrongPlacement
    }
}
