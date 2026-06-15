namespace ERP.ArticleService.Domain
{
    public class Category
    {
        public Guid Id { get; private set; }
        public Guid? TenantId { get; init; }
        public string Name { get; private set; }
        public int TVA { get; private set; }
        public bool IsDeleted { get; private set; } = false;
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        private Category() { }

        public Category(string name, int tva, Guid? tenantId=null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Category name is required");

            if (tva < 0)
                throw new ArgumentException("TVA cannot be below 0");

            Id = Guid.NewGuid();
            Name = name.Trim();
            TVA = tva;
            TenantId = tenantId;
            CreatedAt = DateTime.UtcNow;
        }

        public void Update(string name, int tva)
        {
            if (tva < 0)
                throw new ArgumentException("TVA cannot be below 0");

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Category name is required");

            TVA = tva;
            Name = name.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        public void Delete()
        {
            if (IsDeleted) return;
            IsDeleted = true;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Restore()
        {
            if (!IsDeleted) return;
            IsDeleted = false;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}