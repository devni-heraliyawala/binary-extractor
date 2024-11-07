# Binary Extraction Tool

## Overview
The Binary Extraction Tool is a Windows service that automates the extraction and migration of binary data from a SQL database to Azure Blob Storage. This tool is designed to support the transfer of different attachment types, including **`tblAttachments`**, **`tblGenericAttachments`**, and **`tblWorkOrderAttachments`**, while implementing a sanity check to ensure that all migrated data is successfully stored in the designated Azure storage containers.

## Key Features
* **Automated Binary Data Extraction:** Extracts binary data from SQL database tables and transfers it to Azure Blob Storage.

* **Sanity Check:** Verifies that all files have been successfully transferred to Azure by performing a check on specific external binary IDs.

* **Daily Logging per Database:** Each database has its own log folder in **`Logs/{DatabaseName}`**, with separate files generated daily. This makes tracking and debugging specific database operations easier.

* **Flexible Configuration:** Configurable via **`appsettings.json`** for database connection strings, Azure storage settings, and service parameters.

* **Customizable Retry Policy:** Implements retry policies for failed blob uploads to improve reliability during network interruptions.

* **Abstract Factory Design:** Uses an abstract factory pattern to dynamically create services with database-specific loggers, ensuring all operations are logged accurately within each database's context.

## Technologies Used
* **C#** with **.NET Core (.NET 8)**  for building the Windows service.
* **Microsoft WindowsAzure Storage** for file storage and retrieval.
* **Serilog** for structured logging and dynamic logging based on database context.
* **Dependency Injection** for service management and configuration.
* **Abstract Factory Pattern** for database-specific service creation.

## Architecture
The tool is designed using modular classes for each of its core functionalities:

* **Worker:** Main background service that manages the workflow for each database, invoking multi-threaded migration and sanity checks.

* **MultiThreadingService:** Responsible for concurrent data extraction and blob upload operations to Azure.

* **SanityCheckService:** Verifies the presence of extracted files in Azure storage by performing checks based on specific IDs.

* **DataRepository:** Manages database connections and data retrieval from SQL, specifically the different attachment tables.

* **AzureStorageProviderService:** Facilitates connections to Azure Blob Storage and handles blob upload functionality.

* **DatabaseServiceFactory** and **DatabaseLoggerProvider:** Factories to create instances of services with database-specific loggers.

## Configuration Guide
To set up the tool, adjust the following sections in the `appsettings.json` file:
     
* **ConnectionStrings:** Add connection strings for each database that requires automated data extraction. Name each connection string uniquely, such as "Boris_QA" or "Boris_Revamp".
* **AppSettings:** These settings control data extraction behavior for different attachment types:
   - **DefaultBatchSize:** Sets the general batch size for data processing.
   - **Attachments, GenericAttachments, WorkOrderAttachments:** Each section allows specific configurations:
      - **EnableForMigration:** Turn on/off migration.
      - **ConcurrentCount:** Number of processes running concurrently.
      - **BatchSize:** Sets the number of items processed in one batch.
      - **ChunkSize:** Breaks down a batch into smaller "chunks" for easier handling. For example, if BatchSize is set to 200 and ChunkSize is set to 10, each batch will be processed in chunks of 10 items at a time, which helps manage resources and improve stability during processing

* **Serilog:** Configures logging settings. Logs are directed to the console by default but can also be written to files organized by database name for easier tracking.

## Installation
* Install and configure the service to run on Windows.
* The service will automatically execute its binary extraction and sanity checks based on the settings provided in `appsettings.json`.

## Deployment
### 1. Execute the Windows Service Locally (for testing)
* **Run in Debug Mode:**
   * Open the project in Visual Studio.
   * Set the project as the startup project.
   * Click Start Debugging (F5) to run it as a console application.
   * This is useful for testing and debugging before deploying.

* **Install the Service:**
   * First, publish the project by right-clicking on the project in Visual Studio, selecting Publish, and following the instructions.
   * After publishing, open a command prompt with administrator privileges.
   * Use the `sc` command to install the service:
      ```
      sc create BinaryExtractorAzure binPath= "C:\Path\To\Your\Executable\Boris.BinaryExtraction.Azure.exe"
      ```
   * Replace `C:\Path\To\Your\Executable` with the path to the published `.exe` file.

* **Start the service:**
   * Start the service using the following command:
      ```
      sc start BinaryExtractorAzure
      ```

### 2. Deploy the Windows Service on a Server
* **Copy Files to the Server:**
   * Publish your application (similar to the steps above) and copy the published files to the server.

* **Install the Service on the Server:**
   * Open a command prompt as an administrator.
   * Run the sc create command (as above) to register the service.

* **Configure Service Settings:**
   * Open Services (search for "Services" in the Start Menu).
   * Find your service, right-click, and select **Properties**.
   * Under the **Log On** tab, set the account under which the service should run (e.g., Local System Account).
   * Under the **Recovery** tab, set recovery options if the service fails.

## Schedule the Service to Run at a Specific Time (Using Task Scheduler)
To run the service every Saturday at a specific time, you can use Task Scheduler instead of relying solely on Windows Services:

* **Open Task Scheduler:**
Open the Start Menu, search for **Task Scheduler**, and open it.
* **Create a New Task:**
   * Select **Create Task** from the **Actions** pane.
   * Under the **General** tab, give your task a name, like "Run My Windows Service Every Saturday."
   * Set the task to run using a high-privilege account if needed.

* **Set the Trigger:**
   * Go to the **Triggers** tab and click **New**.
   * Set the **Begin the task** dropdown to **On a schedule**.
   * Choose **Weekly** and select **Saturday** with your desired time.

* **Set the Action:**
   * Go to the **Actions** tab and click **New**.
   * Set the **Action** dropdown to Start a **Program**.
   * In the **Program/script** field, enter the path to `sc.exe`.
   * In the **Add arguments (optional)** field, enter the following arguments to start your service
      ```
      start BinaryExtractorAzure 
      ```
   * Create another action to stop the service if you want to end it after it runs. Use the arguments:
      ```
      stop BinaryExtractorAzure
      ```

* **Save the Task:**
   * Click **OK** to save your task.

Now, Task Scheduler will start your service at the specified time every Saturday.   
## Logging
The tool uses Serilog for logging and creates a separate log file for each database with a daily rolling policy. Logs are stored in the format `Logs/{DatabaseName}/log-YYYYMMDD.txt`.

Log file structure:
* **Information Logs:** Start, progress, and completion messages for each operation.
* **Error Logs:** Detailed error messages for any failed operation, including exception details.
* **Performance Tracking:** Duration of each operation, including extraction, sanity checks, and blob uploads.

## Usage
* **Binary Extraction and Upload:** The service extracts binary data from SQL tables and uploads it to Azure Blob Storage based on configured tables and IDs.

* **Sanity Check:** The tool performs a check to verify if all uploaded files are present in Azure Blob Storage. Missing files are logged as warnings for further investigation.

## Troubleshooting
* **Missing Files in Azure:** Check the logs for any missing files or failed upload attempts. Adjust the retry policy or investigate network issues if required.

* **Connection Errors:** Ensure all connection strings are correct and that the Azure storage account is accessible.

* **Logging Issues:** If logs do not appear as expected, confirm that `DatabaseLoggerProvider` is set up and that `appsettings.json` has correct logging paths and levels.

## License
This project is licensed under the MIT License.