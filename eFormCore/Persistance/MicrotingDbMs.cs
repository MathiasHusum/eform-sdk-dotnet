namespace eFormSqlController
{
    using System.Data.Entity;
    //

    //
    //
    public partial class MicrotingDbMs : DbContext, MicrotingContextInterface
    {
        public MicrotingDbMs() { }

        public MicrotingDbMs(string connectionString)
          : base(connectionString)
        {
        }

        public virtual DbSet<a_interaction_case_list_versions> a_interaction_case_list_versions { get; set; }
        public virtual DbSet<a_interaction_case_lists> a_interaction_case_lists { get; set; }
        public virtual DbSet<a_interaction_case_versions> a_interaction_case_versions { get; set; }
        public virtual DbSet<a_interaction_cases> a_interaction_cases { get; set; }
        public virtual DbSet<case_versions> case_versions { get; set; }
        public virtual DbSet<cases> cases { get; set; }
        public virtual DbSet<check_list_site_versions> check_list_site_versions { get; set; }
        public virtual DbSet<check_list_sites> check_list_sites { get; set; }
        public virtual DbSet<check_list_value_versions> check_list_value_versions { get; set; }
        public virtual DbSet<check_list_values> check_list_values { get; set; }
        public virtual DbSet<check_list_versions> check_list_versions { get; set; }
        public virtual DbSet<check_lists> check_lists { get; set; }
        public virtual DbSet<entity_group_versions> entity_group_versions { get; set; }
        public virtual DbSet<entity_groups> entity_groups { get; set; }
        public virtual DbSet<entity_item_versions> entity_item_versions { get; set; }
        public virtual DbSet<entity_items> entity_items { get; set; }
        public virtual DbSet<field_types> field_types { get; set; }
        public virtual DbSet<field_value_versions> field_value_versions { get; set; }
        public virtual DbSet<field_values> field_values { get; set; }
        public virtual DbSet<field_versions> field_versions { get; set; }
        public virtual DbSet<fields> fields { get; set; }
        public virtual DbSet<log_exceptions> log_exceptions { get; set; }
        public virtual DbSet<logs> logs { get; set; }
        public virtual DbSet<notifications> notifications { get; set; }
        public virtual DbSet<settings> settings { get; set; }
        public virtual DbSet<site_versions> site_versions { get; set; }
        public virtual DbSet<site_worker_versions> site_worker_versions { get; set; }
        public virtual DbSet<site_workers> site_workers { get; set; }
        public virtual DbSet<sites> sites { get; set; }
        public virtual DbSet<unit_versions> unit_versions { get; set; }
        public virtual DbSet<units> units { get; set; }
        public virtual DbSet<uploaded_data> uploaded_data { get; set; }
        public virtual DbSet<uploaded_data_versions> uploaded_data_versions { get; set; }
        public virtual DbSet<worker_versions> worker_versions { get; set; }
        public virtual DbSet<workers> workers { get; set; }
        public virtual DbSet<tags> tags { get; set; }
        public virtual DbSet<tag_versions> tag_versions { get; set; }
        public virtual DbSet<taggings> taggings { get; set; }
        public virtual DbSet<tagging_versions> tagging_versions { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<cases>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<cases>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<cases>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<cases>()
                .Property(e => e.done_at)
                .HasPrecision(0);

            modelBuilder.Entity<cases>()
                .Property(e => e.type)
                .IsUnicode(false);

            modelBuilder.Entity<cases>()
                .Property(e => e.microting_uid)
                .IsUnicode(false);

            modelBuilder.Entity<cases>()
                .Property(e => e.microting_check_uid)
                .IsUnicode(false);

            modelBuilder.Entity<cases>()
                .Property(e => e.case_uid)
                .IsUnicode(false);

            modelBuilder.Entity<cases>()
                .Property(e => e.custom)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_sites>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_sites>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<check_list_sites>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<check_list_sites>()
                .Property(e => e.microting_uid)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_sites>()
                .Property(e => e.last_check_id)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_values>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_values>()
                .Property(e => e.status)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_values>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<check_list_values>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<check_lists>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<check_lists>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<check_lists>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<check_lists>()
                .Property(e => e.label)
                .IsUnicode(false);

            modelBuilder.Entity<check_lists>()
                .Property(e => e.description)
                .HasMaxLength(int.MaxValue)
                .IsUnicode(false);

            modelBuilder.Entity<check_lists>()
                .Property(e => e.custom)
                .IsUnicode(false);

            modelBuilder.Entity<check_lists>()
                .Property(e => e.case_type)
                .IsUnicode(false);

            modelBuilder.Entity<check_lists>()
                .Property(e => e.folder_name)
                .IsUnicode(false);

            modelBuilder.Entity<check_lists>()
                .HasOptional(e => e.parent)
                .WithMany(e => e.children)
                .HasForeignKey(e => e.parent_id)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<uploaded_data>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<uploaded_data>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<uploaded_data>()
                .Property(e => e.expiration_date)
                .HasPrecision(0);

            modelBuilder.Entity<entity_groups>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<entity_groups>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<entity_groups>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<entity_groups>()
                .Property(e => e.microting_uid)
                .IsUnicode(false);

            modelBuilder.Entity<entity_groups>()
                .Property(e => e.name)
                .IsUnicode(false);

            modelBuilder.Entity<entity_groups>()
                .Property(e => e.type)
                .IsUnicode(false);

            modelBuilder.Entity<entity_items>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<entity_items>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<entity_items>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<entity_items>()
                .Property(e => e.entity_group_id)
                .IsUnicode(false);

            modelBuilder.Entity<entity_items>()
                .Property(e => e.entity_item_uid)
                .IsUnicode(false);

            modelBuilder.Entity<entity_items>()
                .Property(e => e.microting_uid)
                .IsUnicode(false);

            modelBuilder.Entity<entity_items>()
                .Property(e => e.name)
                .IsUnicode(false);

            modelBuilder.Entity<entity_items>()
                .Property(e => e.description)
                .IsUnicode(false);

            modelBuilder.Entity<field_types>()
                .Property(e => e.field_type)
                .IsUnicode(false);

            modelBuilder.Entity<field_types>()
                .Property(e => e.description)
                .IsUnicode(false);

            modelBuilder.Entity<field_values>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<field_values>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<field_values>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<field_values>()
                .Property(e => e.done_at)
                .HasPrecision(0);

            modelBuilder.Entity<field_values>()
                .Property(e => e.date)
                .HasPrecision(0);

            modelBuilder.Entity<field_values>()
                .Property(e => e.value)
                .IsUnicode(false);

            modelBuilder.Entity<field_values>()
                .Property(e => e.latitude)
                .IsUnicode(false);

            modelBuilder.Entity<field_values>()
                .Property(e => e.longitude)
                .IsUnicode(false);

            modelBuilder.Entity<field_values>()
                .Property(e => e.altitude)
                .IsUnicode(false);

            modelBuilder.Entity<field_values>()
                .Property(e => e.heading)
                .IsUnicode(false);

            modelBuilder.Entity<field_values>()
                .Property(e => e.accuracy)
                .IsUnicode(false);

            modelBuilder.Entity<fields>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<fields>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<fields>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<fields>()
                .Property(e => e.label)
                .IsUnicode(false);

            modelBuilder.Entity<fields>()
                .Property(e => e.description)
                .HasMaxLength(int.MaxValue)
                .IsUnicode(false);

            modelBuilder.Entity<fields>()
                .Property(e => e.color)
                .IsUnicode(false);

            modelBuilder.Entity<fields>()
                .Property(e => e.default_value)
                .IsUnicode(false);

            modelBuilder.Entity<fields>()
                .Property(e => e.unit_name)
                .IsUnicode(false);

            modelBuilder.Entity<fields>()
                .Property(e => e.min_value)
                .IsUnicode(false);

            modelBuilder.Entity<fields>()
                .Property(e => e.max_value)
                .IsUnicode(false);

            modelBuilder.Entity<fields>()
                .Property(e => e.barcode_type)
                .IsUnicode(false);

            modelBuilder.Entity<fields>()
                .Property(e => e.query_type)
                .IsUnicode(false);

            modelBuilder.Entity<fields>()
                .Property(e => e.key_value_pair_list)
                .IsUnicode(false);

            modelBuilder.Entity<fields>()
                .Property(e => e.custom)
                .IsUnicode(false);

            modelBuilder.Entity<fields>()
                .HasOptional(e => e.parent)
                .WithMany(e => e.children)
                .HasForeignKey(e => e.parent_field_id)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<notifications>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<notifications>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<notifications>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<notifications>()
                .Property(e => e.microting_uid)
                .IsUnicode(false);

            modelBuilder.Entity<notifications>()
                .Property(e => e.transmission)
                .IsUnicode(false);

            modelBuilder.Entity<settings>()
                .Property(e => e.name)
                .IsUnicode(false);

            modelBuilder.Entity<settings>()
                .Property(e => e.value)
                .IsUnicode(false);

            modelBuilder.Entity<sites>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<sites>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<sites>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<site_workers>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<site_workers>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<site_workers>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<units>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<units>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<units>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<workers>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<workers>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<workers>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<workers>()
                .Property(e => e.first_name)
                .IsUnicode(false);

            modelBuilder.Entity<workers>()
                .Property(e => e.last_name)
                .IsUnicode(false);

            modelBuilder.Entity<workers>()
                .Property(e => e.email)
                .IsUnicode(false);

            modelBuilder.Entity<tags>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<tags>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<tags>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<taggings>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<taggings>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<taggings>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<case_versions>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<case_versions>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<case_versions>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<case_versions>()
                .Property(e => e.done_at)
                .HasPrecision(0);

            modelBuilder.Entity<case_versions>()
                .Property(e => e.type)
                .IsUnicode(false);

            modelBuilder.Entity<case_versions>()
                .Property(e => e.microting_uid)
                .IsUnicode(false);

            modelBuilder.Entity<case_versions>()
                .Property(e => e.microting_check_uid)
                .IsUnicode(false);

            modelBuilder.Entity<case_versions>()
                .Property(e => e.case_uid)
                .IsUnicode(false);

            modelBuilder.Entity<case_versions>()
                .Property(e => e.custom)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_site_versions>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_site_versions>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<check_list_site_versions>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<check_list_site_versions>()
                .Property(e => e.microting_uid)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_site_versions>()
                .Property(e => e.last_check_id)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_value_versions>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_value_versions>()
                .Property(e => e.status)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_value_versions>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<check_list_value_versions>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<check_list_versions>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_versions>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<check_list_versions>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<check_list_versions>()
                .Property(e => e.label)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_versions>()
                .Property(e => e.description)
                .HasMaxLength(int.MaxValue)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_versions>()
                .Property(e => e.custom)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_versions>()
                .Property(e => e.case_type)
                .IsUnicode(false);

            modelBuilder.Entity<check_list_versions>()
                .Property(e => e.folder_name)
                .IsUnicode(false);

            modelBuilder.Entity<uploaded_data_versions>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<uploaded_data_versions>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<uploaded_data_versions>()
                .Property(e => e.expiration_date)
                .HasPrecision(0);

            modelBuilder.Entity<entity_group_versions>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<entity_group_versions>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<entity_group_versions>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<entity_group_versions>()
                .Property(e => e.microting_uid)
                .IsUnicode(false);

            modelBuilder.Entity<entity_group_versions>()
                .Property(e => e.name)
                .IsUnicode(false);

            modelBuilder.Entity<entity_group_versions>()
                .Property(e => e.type)
                .IsUnicode(false);

            modelBuilder.Entity<entity_item_versions>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<entity_item_versions>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<entity_item_versions>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<entity_item_versions>()
                .Property(e => e.entity_group_id)
                .IsUnicode(false);

            modelBuilder.Entity<entity_item_versions>()
                .Property(e => e.entity_item_uid)
                .IsUnicode(false);

            modelBuilder.Entity<entity_item_versions>()
                .Property(e => e.microting_uid)
                .IsUnicode(false);

            modelBuilder.Entity<entity_item_versions>()
                .Property(e => e.name)
                .IsUnicode(false);

            modelBuilder.Entity<entity_item_versions>()
                .Property(e => e.description)
                .IsUnicode(false);

            modelBuilder.Entity<field_value_versions>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<field_value_versions>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<field_value_versions>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<field_value_versions>()
                .Property(e => e.done_at)
                .HasPrecision(0);

            modelBuilder.Entity<field_value_versions>()
                .Property(e => e.date)
                .HasPrecision(0);

            modelBuilder.Entity<field_value_versions>()
                .Property(e => e.value)
                .IsUnicode(false);

            modelBuilder.Entity<field_value_versions>()
                .Property(e => e.latitude)
                .IsUnicode(false);

            modelBuilder.Entity<field_value_versions>()
                .Property(e => e.longitude)
                .IsUnicode(false);

            modelBuilder.Entity<field_value_versions>()
                .Property(e => e.altitude)
                .IsUnicode(false);

            modelBuilder.Entity<field_value_versions>()
                .Property(e => e.heading)
                .IsUnicode(false);

            modelBuilder.Entity<field_value_versions>()
                .Property(e => e.accuracy)
                .IsUnicode(false);

            modelBuilder.Entity<field_versions>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<field_versions>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<field_versions>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<field_versions>()
                .Property(e => e.label)
                .IsUnicode(false);

            modelBuilder.Entity<field_versions>()
                .Property(e => e.description)
                .HasMaxLength(int.MaxValue)
                .IsUnicode(false);

            modelBuilder.Entity<field_versions>()
                .Property(e => e.color)
                .IsUnicode(false);

            modelBuilder.Entity<field_versions>()
                .Property(e => e.default_value)
                .IsUnicode(false);

            modelBuilder.Entity<field_versions>()
                .Property(e => e.unit_name)
                .IsUnicode(false);

            modelBuilder.Entity<field_versions>()
                .Property(e => e.min_value)
                .IsUnicode(false);

            modelBuilder.Entity<field_versions>()
                .Property(e => e.max_value)
                .IsUnicode(false);

            modelBuilder.Entity<field_versions>()
                .Property(e => e.barcode_type)
                .IsUnicode(false);

            modelBuilder.Entity<field_versions>()
                .Property(e => e.query_type)
                .IsUnicode(false);

            modelBuilder.Entity<field_versions>()
                .Property(e => e.key_value_pair_list)
                .IsUnicode(false);

            modelBuilder.Entity<field_versions>()
                .Property(e => e.custom)
                .IsUnicode(false);

            modelBuilder.Entity<site_versions>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<site_versions>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<site_versions>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<site_worker_versions>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<site_worker_versions>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<site_worker_versions>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<unit_versions>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<unit_versions>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<unit_versions>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<worker_versions>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<worker_versions>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<worker_versions>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<worker_versions>()
                .Property(e => e.first_name)
                .IsUnicode(false);

            modelBuilder.Entity<worker_versions>()
                .Property(e => e.last_name)
                .IsUnicode(false);

            modelBuilder.Entity<worker_versions>()
                .Property(e => e.email)
                .IsUnicode(false);

            modelBuilder.Entity<tag_versions>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<tag_versions>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<tag_versions>()
                .Property(e => e.updated_at)
                .HasPrecision(0);

            modelBuilder.Entity<tagging_versions>()
                .Property(e => e.workflow_state)
                .IsUnicode(false);

            modelBuilder.Entity<tagging_versions>()
                .Property(e => e.created_at)
                .HasPrecision(0);

            modelBuilder.Entity<tagging_versions>()
                .Property(e => e.updated_at)
                .HasPrecision(0);
        }
    }
}