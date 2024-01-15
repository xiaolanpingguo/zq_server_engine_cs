namespace ZQ
{
    public interface IModule
    {
        public bool Init();
        public bool Update(long timeNow);
        public bool Shutdown();
    }
}
