package com.drohub.thrift.lib;

import org.apache.thrift.TApplicationException;
import org.apache.thrift.TException;
import org.apache.thrift.protocol.TMessage;
import org.apache.thrift.protocol.TProtocol;
import org.apache.thrift.protocol.TProtocolDecorator;
import org.apache.thrift.protocol.TProtocolException;
import org.apache.thrift.protocol.TProtocolFactory;
import org.apache.thrift.transport.TTransport;
import org.apache.thrift.transport.TTransportException;

import java.nio.ByteBuffer;
import java.util.Random;

public class TMessageValidatorProtocol extends TProtocolDecorator {
    /**
     * TMessageValidatorProtocol is a protocol-independent concrete decorator that allows a Thrift
     * client to communicate with a Server and validate that the messaged received is matching the
     * sent seqid. If it does not match we keep reading the buffer or throw an exception.
     * This Protocol Decorator provides the most advantage with a framed transport.
     * By default Thrift processors only provide an incrementing seqid that always starts in 0,
     * which can lead to collisions when 2 protocols are started at the same time over the same
     * transport.
     * To mitigate that this protocol also overrides the seqid normally generated by the processor,
     * because it uses a random seq id to avoid shared channel seqid collisions.
     */
    private final int _MAGIC_NUMBER = 21474347; // 1 71 172 43
    private int _random_seq_id;
    public enum ValidationModeEnum
    {
        KEEP_READING,
        THROW_EXCEPTION
    }

    public enum OperationModeEnum
    {
        SEQID_SLAVE,
        SEQID_MASTER
    }

    public ValidationModeEnum validation_mode;
    public OperationModeEnum operation_mode;
    public TMessageValidatorProtocol(TProtocol protocol, ValidationModeEnum validation_mode, OperationModeEnum operation_mode)
    {
        super(protocol);
        this.validation_mode = validation_mode;
        this.operation_mode = operation_mode;
        Random rand = new Random();
        _random_seq_id = rand.nextInt();
    }

    public static class Factory implements TProtocolFactory {
        private ValidationModeEnum _validation_mode;
        private OperationModeEnum _operation_mode;
        private TProtocolFactory _protocol_factory;
            public Factory(TProtocolFactory protocol_factory, ValidationModeEnum validation_mode, OperationModeEnum operation_mode) {
            _protocol_factory = protocol_factory;
            _validation_mode = validation_mode;
            _operation_mode = operation_mode;
        }

        @Override
        public TProtocol getProtocol(TTransport transport)
        {
            return new TMessageValidatorProtocol(_protocol_factory.getProtocol(transport), _validation_mode, _operation_mode);
        }
    }

    private void readMagicNumber() throws TTransportException
    {
        byte[] HeaderBuffer = new byte[1];
        long result = 0;
        do
        {
            int d = getTransport().readAll(HeaderBuffer, 0, 1);
            result = result << 8 | (int)HeaderBuffer[0] & 0xff;
//            System.out.println(String.format("buffer read %d", (int)HeaderBuffer[0] & 0xff));
//            System.out.println(String.format("buffer result %d", result & 0xFFFFL));
        } while (result != _MAGIC_NUMBER);
    }

    private void  writeMagicNumber() throws TTransportException {
        //Java is already big endian
        byte[] bytes = ByteBuffer.allocate(4).putInt(_MAGIC_NUMBER).array();

        super.getTransport().write(bytes, 0, 4);
    }

    @Override
    public void writeMessageBegin(TMessage message) throws TException
    {
        if (operation_mode == OperationModeEnum.SEQID_MASTER)
        {
            throw new TException("TMessageValidatorProtocol cannot work in SEQID master mode");
//            _random_seq_id++;
//            message.seqid = _random_seq_id;
            //Cannot be implemented in java because seqid is final in JAVA
        }
        else if (operation_mode == OperationModeEnum.SEQID_SLAVE) {
            ;
        }
        else{
            throw new TProtocolException(TProtocolException.NOT_IMPLEMENTED, "Invalid operation mode selected");
        }

        writeMagicNumber();
        super.writeMessageBegin(message);
    }

    @Override
    public TMessage readMessageBegin() throws TException
    {
        readMagicNumber();
        TMessage new_message = super.readMessageBegin();
//        System.out.println($"seq id {_random_seq_id} == {new_message.SeqID}");
        if (operation_mode == OperationModeEnum.SEQID_MASTER)
        {
            while (_random_seq_id != new_message.seqid)
            {
                if (validation_mode == ValidationModeEnum.KEEP_READING)
                {
                    readMagicNumber();
                    new_message = super.readMessageBegin();
                }
                else if (validation_mode == ValidationModeEnum.THROW_EXCEPTION)
                    throw new TApplicationException(TApplicationException.MISSING_RESULT, "Received SeqID and sent one do not match.");
                else
                    throw new TException("This is an unreachable situation");
            }
        }
        return new_message;
    }
}