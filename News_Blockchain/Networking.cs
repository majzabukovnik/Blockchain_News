using System.Net;
using System.Net.Sockets;
using System.Text;

namespace News_Blockchain;

public struct Peers
{
    
}

public class Networking
{
    
    
    public Networking()
    {
        
    }
    
    public async Task<Message> Connect(byte[] ip, Message msg)
    {

        try
        {
            IPEndPoint ipEndPoint = new IPEndPoint(new IPAddress(ip), 1201);
        
            using Socket client = new(
                ipEndPoint.AddressFamily, 
                SocketType.Stream, 
                ProtocolType.Tcp);

            await client.ConnectAsync(ipEndPoint);
            while (true)
            {
                // Send message.
                var message = Serializator.SerializeToString(msg);
                var messageBytes = Encoding.UTF8.GetBytes(message);
                //serializacija celotnega objekta, ne bo stringov
            
                _ = await client.SendAsync(messageBytes, SocketFlags.None);
                //Console.WriteLine($"Socket client sent message: \"{message}\"");

                if (msg.GetMessageType() == typeof(Request))
                {
                    var buffer = new byte[1_024];
                    var received = await client.ReceiveAsync(buffer, SocketFlags.None);
                    var response = Encoding.UTF8.GetString(buffer, 0, received);
                    var deserialized = Serializator.DeserializeMessage(response);
                    return deserialized;
                }
                else
                {
                    // Receive ack.
                    var buffer = new byte[1_024];
                    var received = await client.ReceiveAsync(buffer, SocketFlags.None);
                    var response = Encoding.UTF8.GetString(buffer, 0, received);
                    if (response == "<|ACK|>")
                    {
                        return new Message();
                    }
                }
            }
            client.Shutdown(SocketShutdown.Both);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return new Message();
        }
    }

    public async Task<string> Listen(byte[] ip)
    {
        IPEndPoint ipEndPoint = new IPEndPoint(new IPAddress(ip), 1201);
        
        using Socket listener = new(
            ipEndPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);

        
        listener.Bind(ipEndPoint);
        listener.Listen(100);
        
        var handler = await listener.AcceptAsync();
        while (true)
        {
            // Receive message.
            var buffer = new byte[1_024];
            var received = await handler.ReceiveAsync(buffer, SocketFlags.None);
            var response = Encoding.UTF8.GetString(buffer, 0, received);
            Message msg = Serializator.DeserializeMessage(response);


            if (msg.GetMessageType() == typeof(Request))
            {
                //TODO: napisi kodo, ki iz database potegne block (treba se posodobit database modul)
                var ackMessage = "<|ACK|>";
                var echoBytes = Encoding.UTF8.GetBytes(ackMessage);
                await handler.SendAsync(echoBytes, 0);
                Console.WriteLine(
                    $"Socket server sent acknowledgment: \"{ackMessage}\"");
                break;
            }
            else
            {

                //TODO: Prejeto transakcijo/block treba validirat in nato dati v db in poslati naprej
                var ackMessage = "<|ACK|>";
                var echoBytes = Encoding.UTF8.GetBytes(ackMessage);
                await handler.SendAsync(echoBytes, 0);
                Console.WriteLine(
                    $"Socket server sent acknowledgment: \"{ackMessage}\"");

                break;
            }
        }

        return ""; 
    }
    
}