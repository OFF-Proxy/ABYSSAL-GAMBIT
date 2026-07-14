namespace AutoChessBossRush.Save
{
    public interface ISaveStore
    {
        SaveData Load();
        void Save(SaveData data);
        bool Exists();
        void Delete();
    }
}
