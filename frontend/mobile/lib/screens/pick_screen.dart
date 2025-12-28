import 'package:flutter/material.dart';

class PickScreen extends StatelessWidget {
  const PickScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Pick Tasks'),
      ),
      body: ListView.builder(
        itemCount: 5,
        itemBuilder: (context, index) {
          return Card(
            margin: const EdgeInsets.all(8),
            child: ListTile(
              leading: const CircleAvatar(
                child: Icon(Icons.assignment),
              ),
              title: Text('Task #${index + 1}'),
              subtitle: Text('Item: RM-00${index + 1}\nLocation: STG-A-0${index + 1}'),
              trailing: ElevatedButton(
                onPressed: () {},
                child: const Text('Pick'),
              ),
            ),
          );
        },
      ),
    );
  }
}
