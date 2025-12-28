import 'package:flutter/material.dart';

class IssueScreen extends StatelessWidget {
  const IssueScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Issue to Production'),
      ),
      body: const Center(
        child: Text('Issue Material to Work Order'),
      ),
    );
  }
}
