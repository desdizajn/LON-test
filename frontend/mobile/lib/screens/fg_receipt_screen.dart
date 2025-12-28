import 'package:flutter/material.dart';

class FGReceiptScreen extends StatelessWidget {
  const FGReceiptScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('FG Receipt'),
      ),
      body: const Center(
        child: Text('Receive Finished Goods from Production'),
      ),
    );
  }
}
