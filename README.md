# EntityFrameworkCoreExtensions

动态查询扩展（暂时未对模型可空属性做处理）

由于本人不是很会表达，如有描述不清晰或不明白的请发邮件（tangly1991@foxmail.com）告知，谢谢

## 安装

通过NUGET安装 `Ame.EntityFrameworkCore.Extensions`

在 `Startup.cs` `ConfigureServices()` 里添加如下代码

```C#
DynamicQueryableExtended.ModelNamespace = "你的模型所属命名空间";
```

# Usage

* **Entity**

```C#
public enum LinqSelectOperator
{
    Contains, // 包含
    Equal, // 等于
    Greater, // 大于
    GreaterEqual, // 大于等于
    Less, // 小于
    LessEqual, // 小于等于
    NotEqual, // 不等于
    InWithEqual,  // 对于多个值执行等于比较
    InWithContains,// 对于多个值执行包含比较
    Between, // 范围
}

[Table("Posts")]
public class Post
{
    [Column(IsPrimaryKey = true)]
    [AutoIncrement]
    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public ICollection<Tag> Terms { get; set; } = new List<Tag>();
}

public class Tag
{
    [Column(IsPrimaryKey = true)]
    public int Id { get; set; }
    public string Name { get; set; }
}
```

* **Select**

查询文章标题及文章所属标签名称

```C#
dbContext.Select("Title,Terms.Name");
```

* **Where**

查询文章时显示符合条件的文章标签

```C#
dbContext.Where(new List<LinqSelectCondition>
{
    Field = "Terms.Name",
    Value = "aa,bb,cc",
    Operator = LinqSelectOperator.InWithEqual
});

dbContext.Where(new List<LinqSelectCondition>
{
    Field = "Terms.Name",
    Value = "aa,bb,cc",
    Operator = LinqSelectOperator.InWithContains
});

dbContext.Where(new List<LinqSelectCondition>
{
    Field = "Terms.Id",
    Value = "1,5",
    Operator = LinqSelectOperator.Between
});
```
