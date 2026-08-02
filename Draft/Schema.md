# 使用者模組

```mermaid
erDiagram
    Account{
       varchar(200)  AccountID PK "使用者帳號"
       varchar(200)  Password "密碼"
       varchar(200)  UserID "使用者ID"
    }

    User{
       varchar(200)  UserID PK "使用者ID"
       varchar(200)  Name "姓名"
       varchar(200)  Email "電子郵件"
       varchar(200)  Remark "備註"
    }

    Role{
       varchar(200)  RoleID PK "角色ID"
       varchar(200)  Name "名稱"
    }

    UserRole{
       varchar(200)  UserID PK "使用者ID"
       varchar(200)  RoleID PK "角色ID"
       bit(1)        IsEnable "是否啟用"
    }

    RoleFunction{
       varchar(200)  RoleID PK "角色ID"
       varchar(200)  FunctionID PK "功能ID"
       bit(1)        IsEnable "是否啟用"
    }

    Function{
      varchar(200)  FunctionID PK "功能ID"
      varchar(200)  Name           "名稱"
    }

    Account ||--o{ User : "帳號與使用者資料"
    User |{--o{ UserRole : "使用者與使用者角色"
    Role |{--o{ RoleFunction : "角色與功能"
```

# 表單模組
```mermaid
erDiagram
WorkItem{
      varchar(200)  WorkItemID PK   "工作ID"
      varchar(200)  CreateUserID PK "建立使用者ID"
      varchar(200)  AsignUserID PK  "指派使用者ID"
      varchar(200)  Title           "標題"
      varchar(200)  Description     "描述"
      DateTime      CreateAt        "建立時間"
      varchar(200)  Status          "狀態"
      bit           IsDeleted       "是否刪除"
}

WorkItem_History{
      varchar(200)  WorkItemID PK   "工作ID"
      bigint HistoryID PK           "自動遞增"
      varchar(200)  Action          "動作(增加、修改、刪除)"
      varchar(200)  CreateUserID PK "建立使用者ID"
      varchar(200)  AsignUserID PK  "指派使用者ID"
      varchar(200)  Title           "標題"
      varchar(200)  Description     "描述"
      DateTime      CreateAt        "建立時間"
      varchar(200)  WorkItemStatusID"狀態"
}

WorkItemStatus{
    varchar(200)  WorkItemStatusID "工作狀態ID"
    varchar(200)  Name              "名稱"
}

Action{
    varchar(200)  ActionID "動作ID"
    varchar(200)  Name     "名稱"
}

WorkItem |{--o{ WorkItem_History : 任務開立紀錄


```
