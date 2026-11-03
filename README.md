# SCADA Monitoring and Control Application

## Overview

This project is a SCADA (Supervisory Control and Data Acquisition) application developed using C# and Windows Forms to explore industrial automation, PLC communication, and real-time data monitoring.

The project was developed in a simulated environment to test communication between PLC systems and a custom software application.

## Project Development

To test the application, I configured a Siemens S7-300 PLC using TIA Portal and simulated its operation with PLCSIM.

I used NetToPLCSim and Kepware OPC UA Server to establish communication between the simulated PLC and the SCADA application.

The application was designed to read and display PLC variables, monitor process data, and provide a user interface for interacting with the simulated system.

A separate Python-based OPC UA simulation environment was also developed for communication testing.

## Features

- OPC UA communication with industrial data sources
- Dynamic monitoring of PLC variables
- Real-time data visualization using Windows Forms
- Process monitoring through interactive interface components
- Historical data logging using SQL Server
- Support for testing with simulated PLC environments

## Technologies Used

- **Programming Languages:** C#, Python
- **Framework:** .NET 8, Windows Forms
- **Industrial Automation:** Siemens TIA Portal, S7-PLCSIM
- **Communication:** OPC UA, Kepware, NetToPLCSim
- **Database:** Microsoft SQL Server
- **Development Environment:** Visual Studio

## System Architecture

The testing environment consisted of the following components:

1. **TIA Portal and PLCSIM:** Configuration and simulation of the Siemens S7-300 PLC.
2. **NetToPLCSim:** Communication bridge between the simulated PLC and external applications.
3. **Kepware OPC UA Server:** Exposure of PLC variables through OPC UA.
4. **C# SCADA Application:** Monitoring and visualization of process data.
5. **SQL Server:** Storage of historical process data.

The components were tested in a virtualized environment to explore communication between separate systems.

## Project Scope

This project was developed for learning, experimentation, and software integration testing.

It demonstrates practical experience with PLC simulation, OPC UA communication, SCADA interface development, and industrial data monitoring.

**Note:** The project was developed and tested in a simulated environment. It is not intended to represent a production-deployed industrial control system.
