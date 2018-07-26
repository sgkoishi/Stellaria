# Stellaria
A multi-world plugin for TShock. It forward all packets from some players to `room server`.  
Use `/sv [name]` to switch to different world.  
Permission for using `/sv` is `chireiden.stellaria.use`. 

### Config File
By default, a config file will be created.  
* "Host": true if it is host server.  
* "Key": A private key to verify server.
  * Key can be same for different server.
  * If Key in Host's `stellatia.json -> Servers` does not match room server's Key, players still can join but room server can't get the real IP address of player.  
* "Name": Displayed name. Player use this name to join, so don't be too complex.  
  * **There must be a server in `Servers` have same `Name` as current server's Name.**   
  * Name in `Servers` should be unique.  
* "JoinBytes": These bytes contain version information of Terraria. *Don't change it unless game update or modified client.*  
* "Servers": A list of all server it can forward. Contains itself.  
* "Permission": Permission required to join to this world.  
* "OnEnter": Not implemented yet.  
* "OnLeave": Not implemented yet.  
* "GlobalCommands": These commands will be handled by host server, even if they are forwarded.

#### Sample config
Server 7777 (Host):

    {
      "Host": true,
      "Key": "aAdgfl52k8OamHRtrWsvbhJMXlcT6dhF9PuLur91mEA=", // Key 1, random generated
      "Name": "lobby", // Name 1
      "JoinBytes": "AQtUZXJyYXJpYTE5NA==",
      "Servers": [
        {
          "Address": "127.0.0.1",
          "Port": 7777,
          "Name": "lobby", // One of Name in Servers must be same with Name 1
          "Permission": "",
          "OnEnter": [],
          "OnLeave": [],
          "GlobalCommands": [
            "sv",
            "who"
          ],
          "SpawnX": 1000,
          "SpawnY": 300,
          "Key": "aAdgfl52k8OamHRtrWsvbhJMXlcT6dhF9PuLur91mEA="
        },
        {
          "Address": "127.0.0.1",
          "Port": 7778,
          "Name": "s2", // Name 2
          "Permission": "",
          "OnEnter": [],
          "OnLeave": [],
          "GlobalCommands": [
            "sv",
            "who"
          ],
          "SpawnX": 1000,
          "SpawnY": 300,
          "Key": "ADNzptEEsyuuZZxRWPPUawPKi2rJIUU3ahv7n107DuE=" // Key 2
        }
      ]
    }

Server 7778 (Room):

    {
      "Host": false,
      "Key": "ADNzptEEsyuuZZxRWPPUawPKi2rJIUU3ahv7n107DuE=", // Same as key 2 to recieve real IP
      "Name": "s2", // It should be same as Name 2 but nobody cares
      "JoinBytes": "AQtUZXJyYXJpYTE5NA==",
      "Servers": [] // It is not host server, and it won't forward anything.
    }