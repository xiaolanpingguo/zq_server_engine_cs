namespace ZQ
{
    public interface IComponent
    {
        public bool Init();
        public bool Update(long timeNow);
        public bool Shutdown();
    }
}
