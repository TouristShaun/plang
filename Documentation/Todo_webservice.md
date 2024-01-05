# Todo Webservice Guide

This guide will walk you through the process of creating a Todo web service using the Plang programming language. 

![Todo Webservice](https://img.youtube.com/vi/oE3NC4zmRA4/0.jpg)

You can also watch it in [this video](https://www.youtube.com/watch?v=abew4btk34). There might be some insights that are not in the written guide.

## Prerequisites

- Plang installed & IDE setup [installed on your system](Install.md)
- Optional: A REST client for testing the API endpoints

## Steps

### 1. Create a Project Directory

Create a new directory named `Todo` at your preferred location. For instance, you can create it at `c:\plang\Todo` on Windows.

For Linux and MacOS, you can create it at `~/plang/Todo`.

### 2. Create `Setup.goal`

In the `Todo` directory, create a new file named `Setup.goal` and add the following code:

```plang
Setup
- create table Todos 
    columns:    task(string, not null), due_date(datetime, not null), 
                completed(bool, false), created(datetime, now)
```

Note: Plang will automatically create and manage an `id` column for the table. By allowing Plang to handle `id` you enable syncing between devices. It's a technique called Event sourcing. You don't need to know anything about at this stage.

### 3. Build and Run `Setup.goal`

Execute the `Setup.goal` file to create the `Todos` table in the database.

```bash
plang exec Setup
```

### 4. Create `Start.goal`

In the `Todo` directory, create a new file named `Start.goal` and add the following code:

```plang
Start
- start webserver
```
This will create a web server running on your computer, at http://localhost:8080 (link will not work just yet).

### 5. Create `api` Directory

In the `Todo` directory, create a new directory named `api`. `api` stands for application programming interface, it is how computers communicate between them.

### 6. Create `NewTask.goal`

In the `api` directory, create a new file named `NewTask.goal` and add the following code:

```plang
NewTask
- make sure that %task% and %due_date% is not empty, throw error
- insert into Todos %task%, %due_date%, write to %id%
- write out %id%
```

### 7. Create `List.goal`

In the `api` directory, create a new file named `List.goal` and add the following code:

```plang
List
- select everything from Todos, write to %todos%
- write out %todos%
```
Note: if you are familiar with SQL, you could also write `select * from Todos`, it is just in a more natural language

### 8. Build and Run the Code
If you are using VS Code, then press F5 on your keyboard to build and run the code.

If you prefer terminal, navigate to the root of the `Todo` directory and execute the following command:

```bash
plang exec
```

This will start a web server with the logic you've defined.

### 9. Test the API Endpoints

Lets create some data in your Todo list.

- Create `test` folder in the root of `Todo`
- Create a new `TestNewTask.goal` file in the `test` directory with the following code:
    ```plang
    TestNewTask
    - post http://localhost:8080/api/newtask
        {
            "task":"Do homework",
            "due_date": "2023-27-12"
        }
        write to %result%
    - write out %result%
    ```

Then, execute the `TestNewTask.goal` file:

- Press F5 in VS Code, in the prompt window type in `TestNewTask` and press enter
- or if you prefer terminal

    ```bash
    plang exec test/TestNewTask
    ```

You should receive the `id` of the new task in the response.

Alternativly, you can test the API endpoints using your favorite REST client.

To create a new task, send a POST request to `http://localhost:8080/api/newtask` with the following JSON body:

```json
{
    "task":"Do homework",
    "due_date": "2023-27-12"
}
```

To retrieve the list of tasks, send a GET request to [http://localhost:8080/api/list](http://localhost:8080/api/list). This should return a list of tasks you've created.

Alternatively, you can create a new `GetList.goal` file in the `test` directory with the following code:

```plang
GetList 
- get http://localhost:8080/api/list, write to %todos%
- write out %todos%
```

- Press F5 in VS Code, in the prompt window type in `GetList` and press enter
- or if you prefer terminal
    ```bash
    plang exec test/GetList
    ```

Next, [add LLM to this todo app](./Todo_Llm.md)