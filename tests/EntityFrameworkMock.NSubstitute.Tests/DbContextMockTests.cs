﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EntityFrameworkMock.Tests.Models;
using NUnit.Framework;

namespace EntityFrameworkMock.NSubstitute.Tests
{
    [TestFixture]
    public class DbContextMockTests
    {
        [Test]
        public void DbContextMock_Constructor_PassConnectionString_ShouldPassConnectionStringToMockedClass()
        {
            // Arrange
            var connectionString = @"Server=myServerName\myInstanceName;Database=myDataBase;User Id=myUsername; Password = myPassword;";

            // Act
            var dbContextMock = new DbContextMock<TestDbContext>(connectionString);

            // Assert
            Assert.That(dbContextMock.DbContextObject.Database.Connection.ConnectionString, Is.EqualTo(connectionString));
        }

        [Test]
        public async Task DbContextMock_Constructor_ShouldSetupSaveChanges()
        {
            // Arrange
            var dbContextMock = new DbContextMock<TestDbContext>("abc");

            // Act
            dbContextMock.RegisterDbSetMock(x => x.Users, new TestDbSetMock());

            // Assert
            Assert.That(dbContextMock.DbContextObject.SaveChanges(), Is.EqualTo(55861));
            Assert.That(await dbContextMock.DbContextObject.SaveChangesAsync(), Is.EqualTo(55861));
            Assert.That(await dbContextMock.DbContextObject.SaveChangesAsync(CancellationToken.None), Is.EqualTo(55861));
        }

        [Test]
        public void DbContextMock_Reset_ShouldForgetMockedDbSets()
        {
            // Arrange
            var dbContextMock = new DbContextMock<TestDbContext>("abc");
            dbContextMock.RegisterDbSetMock(x => x.Users, new TestDbSetMock());
            Assert.That(dbContextMock.DbContextObject.SaveChanges(), Is.EqualTo(55861));

            // Act
            dbContextMock.Reset();

            // Assert
            Assert.That(dbContextMock.DbContextObject.SaveChanges(), Is.EqualTo(0));
        }

        [Test]
        public void DbContextMock_Reset_ShouldResetupSaveChanges()
        {
            // Arrange
            var dbContextMock = new DbContextMock<TestDbContext>("abc");
            dbContextMock.RegisterDbSetMock(x => x.Users, new TestDbSetMock());
            Assert.That(dbContextMock.DbContextObject.SaveChanges(), Is.EqualTo(55861));
            dbContextMock.Reset();
            Assert.That(dbContextMock.DbContextObject.SaveChanges(), Is.EqualTo(0));

            // Act
            dbContextMock.RegisterDbSetMock(x => x.Users, new TestDbSetMock());

            // Assert
            Assert.That(dbContextMock.DbContextObject.SaveChanges(), Is.EqualTo(55861));
        }

        [Test]
        public void DbContextMock_CreateDbSetMock_CreateIdenticalDbSetMockTwice_ShouldThrowExceptionSecondTime()
        {
            // Arrange
            var dbContextMock = new DbContextMock<TestDbContext>("abc");
            dbContextMock.CreateDbSetMock(x => x.Users);

            // Act
            void CreateDbMock()
            {
                dbContextMock.CreateDbSetMock(x => x.Users);
            }

            // Assert
            var ex = Assert.Throws<ArgumentException>(CreateDbMock);
            Assert.That(ex.ParamName, Is.EqualTo("dbSetSelector"));
            Assert.That(ex.Message, Does.StartWith("DbSetMock for Users already created"));
        }

        [Test]
        public void DbContextMock_CreateDbSetMock_ShouldSetupMockForDbSetSelector()
        {
            var dbContextMock = new DbContextMock<TestDbContext>("abc");
            Assert.That(dbContextMock.DbContextObject.Users, Is.Null);
            dbContextMock.CreateDbSetMock<User>(x => x.Users);
            Assert.That(dbContextMock.DbContextObject.Users, Is.Not.Null);
        }

        [Test]
        public async Task DbContextMock_CreateDbSetMock_PassInitialEntities_DbSetShouldContainInitialEntities()
        {
            // Arrange
            var dbContextMock = new DbContextMock<TestDbContext>("abc");

            // Act
            dbContextMock.CreateDbSetMock(x => x.Users, new[]
            {
                new User { Id = Guid.NewGuid(), FullName = "Eric Cartoon" },
                new User { Id = Guid.NewGuid(), FullName = "Billy Jewel" },
            });

            // Assert
            Assert.That(dbContextMock.DbContextObject.Users.Count(), Is.EqualTo(2));
            Assert.That(await dbContextMock.DbContextObject.Users.CountAsync(), Is.EqualTo(2));

            var result = await dbContextMock.DbContextObject.Users.FirstAsync(x => x.FullName.StartsWith("Eric"));
            Assert.That(result.FullName, Is.EqualTo("Eric Cartoon"));

            result = dbContextMock.DbContextObject.Users.First(x => x.FullName.Contains("Jewel"));
            Assert.That(result.FullName, Is.EqualTo("Billy Jewel"));
        }

        [Test]
        public void DbContextMock_CreateDbSetMock_NoKeyFactoryForModelWithoutKeyAttributes_ShouldThrowException()
        {
            var dbContextMock = new DbContextMock<TestDbContext>("abc");
            var ex = Assert.Throws<InvalidOperationException>(() => dbContextMock.CreateDbSetMock(x => x.NoKeyModels));
            Assert.That(ex.Message, Is.EqualTo("Entity type NoKeyModel does not contain any property marked with KeyAttribute"));
        }

        [Test]
        public void DbContextMock_CreateDbSetMock_CustomKeyFactoryForModelWithoutKeyAttributes_ShouldNotThrowException()
        {
            var dbContextMock = new DbContextMock<TestDbContext>("abc");
            Assert.DoesNotThrow(() => 
                dbContextMock.CreateDbSetMock(
                    x => x.NoKeyModels, (x, _) => x.Id));
        }

        [Test]
        public void DbContextMock_CreateDbSetMock_AddModelWithSameKeyTwice_ShouldThrowDbUpdatedException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var dbContextMock = new DbContextMock<TestDbContext>("abc");
            var dbSetMock = dbContextMock.CreateDbSetMock(x => x.Users);
            dbSetMock.DbSet.Add(new User { Id = userId, FullName = "SomeName" });
            dbSetMock.DbSet.Add(new User { Id = Guid.NewGuid(), FullName = "SomeName" });
            dbContextMock.DbContextObject.SaveChanges();        
            dbSetMock.DbSet.Add(new User { Id = userId, FullName = "SomeName" });

            // Act
            void CallSaveChanges()
            {
                dbContextMock.DbContextObject.SaveChanges();
            }

            // Assert
            Assert.Throws<DbUpdateException>(CallSaveChanges);
        }

        [Test]
        public void DbContextMock_CreateDbSetMock_DeleteUnknownModel_ShouldThrowDbUpdateConcurrencyException()
        {
            var dbContextMock = new DbContextMock<TestDbContext>("abc");
            var dbSetMock = dbContextMock.CreateDbSetMock(x => x.Users);
            dbSetMock.DbSet.Remove(new User { Id = Guid.NewGuid() });
            Assert.Throws<DbUpdateConcurrencyException>(() => dbContextMock.DbContextObject.SaveChanges());
        }

        [Test]
        public void DbContextMock_CreateDbSetMock_AddMultipleModelsWithDatabaseGeneratedIdentityKey_ShouldGenerateSequentialKey()
        {
            var dbContextMock = new DbContextMock<TestDbContext>("abc");
            var dbSetMock = dbContextMock.CreateDbSetMock(x => x.GeneratedKeyModels, new[]
            {
                new GeneratedKeyModel {Value = "first"},
                new GeneratedKeyModel {Value = "second"}
            });
            dbSetMock.DbSet.Add(new GeneratedKeyModel { Value = "third" });
            dbContextMock.DbContextObject.SaveChanges();

            Assert.That(dbSetMock.DbSet.Min(x => x.Id), Is.EqualTo(1));
            Assert.That(dbSetMock.DbSet.Max(x => x.Id), Is.EqualTo(3));
            Assert.That(dbSetMock.DbSet.First(x => x.Id == 1).Value, Is.EqualTo("first"));
            Assert.That(dbSetMock.DbSet.First(x => x.Id == 2).Value, Is.EqualTo("second"));
            Assert.That(dbSetMock.DbSet.First(x => x.Id == 3).Value, Is.EqualTo("third"));
        }


        [Test]
        public void DbContextMock_MultipleGets_ShouldReturnDataEachTime()
        {
            // Arrange
            var users = new List<User>()
            {
                new User() { Id = Guid.NewGuid(), FullName = "Ian Kilmister" },
                new User() { Id = Guid.NewGuid(), FullName = "Phil Taylor" },
                new User() { Id = Guid.NewGuid(), FullName = "Eddie Clarke" }
            };

            var dbContextMock = new DbContextMock<TestDbContext>("abc");
            dbContextMock.CreateDbSetMock(x => x.Users, users);

            List<string> results = new List<string>();

            // Act
            for (int i = 1; i <= 100; i++)
            {
                var readUsers = dbContextMock.DbContextObject.Users;
                foreach (var user in readUsers)
                {
                    results.Add(user.FullName);
                }
            }

            // Assert            
            Assert.AreEqual(300, results.Count());
            foreach (var result in results)
            {
                Assert.IsNotEmpty(result);
            }
        }

        public class TestDbSetMock : IDbSetMock
        {
            public int SaveChanges() => 55861;
        }

        public class TestDbContext : DbContext
        {
            public TestDbContext(string connectionString)
                : base(connectionString)
            {
                ConnectionString = connectionString;
            }

            public string ConnectionString { get; }

            public virtual DbSet<User> Users { get; set; }

            public virtual DbSet<NoKeyModel> NoKeyModels { get; set; }

            public virtual DbSet<GeneratedKeyModel> GeneratedKeyModels { get; set; }
        }
    }
}